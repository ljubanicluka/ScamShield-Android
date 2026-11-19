package rs.scamshield.app

import android.app.Activity
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.webkit.JavascriptInterface
import android.webkit.ValueCallback
import android.webkit.WebChromeClient
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebSettings
import android.webkit.WebView
import android.webkit.WebViewClient
import android.widget.Toast
import androidx.webkit.WebViewAssetLoader
import com.google.mlkit.vision.barcode.Barcode
import com.google.mlkit.vision.barcode.BarcodeScanner
import com.google.mlkit.vision.barcode.BarcodeScannerOptions
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.common.InputImage
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Hosts the bundled ScamShield web UI.
 *
 * QR scanning is deliberately native instead of relying on WebView getUserMedia:
 * - Camera scanning runs in QrScannerActivity using CameraX + the bundled ML Kit model.
 * - Gallery images are opened with Android's system document picker and decoded locally.
 * - QR content is passed back to the page as JSON-quoted text; it is never uploaded.
 *
 * External links are opened by Android, not loaded inside the restricted WebView.
 */
class MainActivity : Activity() {
    private val assetHost = "appassets.androidplatform.net"
    private lateinit var web: WebView
    private var fileCallback: ValueCallback<Array<Uri>>? = null
    private lateinit var advisoryService: LiveAdvisoryService
    private val feedRefreshRunning = AtomicBoolean(false)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        web = WebView(this)
        advisoryService = LiveAdvisoryService(this)
        setContentView(web)

        val loader = WebViewAssetLoader.Builder()
            .addPathHandler("/assets/", WebViewAssetLoader.AssetsPathHandler(this))
            .build()

        web.settings.javaScriptEnabled = true
        web.settings.domStorageEnabled = true
        web.settings.allowFileAccess = false
        web.settings.allowContentAccess = true
        web.settings.mediaPlaybackRequiresUserGesture = true
        web.settings.javaScriptCanOpenWindowsAutomatically = false
        web.settings.setSupportMultipleWindows(false)
        web.settings.mixedContentMode = WebSettings.MIXED_CONTENT_NEVER_ALLOW
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            web.settings.safeBrowsingEnabled = true
        }

        web.webViewClient = object : WebViewClient() {
            override fun shouldInterceptRequest(view: WebView, request: WebResourceRequest): WebResourceResponse? {
                val uri = request.url
                return if (uri.scheme == "https" &&
                    uri.host.equals(assetHost, ignoreCase = true) &&
                    uri.path?.startsWith("/assets/") == true
                ) {
                    loader.shouldInterceptRequest(uri)
                } else {
                    // All UI assets are bundled. Public advisories are fetched by the native service.
                    WebResourceResponse(
                        "text/plain",
                        "utf-8",
                        java.io.ByteArrayInputStream(ByteArray(0))
                    )
                }
            }

            override fun shouldOverrideUrlLoading(view: WebView, request: WebResourceRequest): Boolean {
                val uri = request.url
                val scheme = uri.scheme?.lowercase() ?: return true
                if (scheme == "https" &&
                    uri.host.equals(assetHost, ignoreCase = true) &&
                    uri.path?.startsWith("/assets/") == true
                ) return false

                if (scheme !in setOf("https", "http", "tel", "mailto")) return true
                return try {
                    startActivity(Intent(Intent.ACTION_VIEW, uri))
                    true
                } catch (_: Exception) {
                    Toast.makeText(
                        this@MainActivity,
                        "No app is available to open this link.",
                        Toast.LENGTH_SHORT
                    ).show()
                    true
                }
            }
        }

        web.addJavascriptInterface(object {
            @JavascriptInterface
            fun getCachedAdvisories(): String = advisoryService.cachedSnapshot().toString()

            @JavascriptInterface
            fun refreshAdvisories() {
                if (!feedRefreshRunning.compareAndSet(false, true)) return
                Thread {
                    val result = try {
                        advisoryService.refresh().toString()
                    } catch (_: Exception) {
                        advisoryService.cachedSnapshot().toString()
                    }
                    runOnUiThread {
                        feedRefreshRunning.set(false)
                        if (::web.isInitialized && !isFinishing && !isDestroyed) {
                            val quoted = org.json.JSONObject.quote(result)
                            web.evaluateJavascript(
                                "window.onLiveAdvisories && window.onLiveAdvisories(JSON.parse($quoted));",
                                null
                            )
                        }
                    }
                }.start()
            }

            @JavascriptInterface
            fun launchQrScanner() {
                runOnUiThread {
                    try {
                        startActivityForResult(
                            Intent(this@MainActivity, QrScannerActivity::class.java),
                            REQUEST_QR_CAMERA
                        )
                    } catch (_: Exception) {
                        sendQrResult(
                            "onNativeQrScanResult",
                            null,
                            "Could not open the camera scanner. Use the gallery option instead."
                        )
                    }
                }
            }

            @JavascriptInterface
            fun pickQrImage() {
                runOnUiThread {
                    try {
                        val intent = Intent(Intent.ACTION_OPEN_DOCUMENT).apply {
                            addCategory(Intent.CATEGORY_OPENABLE)
                            type = "image/*"
                            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                        }
                        startActivityForResult(intent, REQUEST_QR_IMAGE)
                    } catch (_: Exception) {
                        sendQrResult(
                            "onNativeQrImageResult",
                            null,
                            "Could not open the image picker."
                        )
                    }
                }
            }

            @JavascriptInterface
            fun openAppSettings() {
                runOnUiThread {
                    try {
                        val intent = Intent(
                            Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                            Uri.parse("package:" + packageName)
                        )
                        startActivity(intent)
                    } catch (_: Exception) {
                        Toast.makeText(
                            this@MainActivity,
                            "Could not open Android app settings.",
                            Toast.LENGTH_SHORT
                        ).show()
                    }
                }
            }
        }, "ScamShieldNative")

        web.webChromeClient = object : WebChromeClient() {
            override fun onShowFileChooser(
                view: WebView,
                callback: ValueCallback<Array<Uri>>,
                params: FileChooserParams
            ): Boolean {
                fileCallback?.onReceiveValue(null)
                fileCallback = callback
                return try {
                    startActivityForResult(params.createIntent(), REQUEST_WEB_FILE)
                    true
                } catch (_: Exception) {
                    fileCallback = null
                    callback.onReceiveValue(null)
                    Toast.makeText(
                        this@MainActivity,
                        "Could not open the file picker.",
                        Toast.LENGTH_SHORT
                    ).show()
                    true
                }
            }
        }

        web.loadUrl("https://appassets.androidplatform.net/assets/index.html")
    }

    @Deprecated("Deprecated in Java")
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)

        when (requestCode) {
            REQUEST_WEB_FILE -> {
                val result = if (resultCode == RESULT_OK) {
                    WebChromeClient.FileChooserParams.parseResult(resultCode, data)
                } else {
                    null
                }
                fileCallback?.onReceiveValue(result)
                fileCallback = null
            }

            REQUEST_QR_CAMERA -> {
                if (resultCode == RESULT_OK) {
                    val payload = data?.getStringExtra(QrScannerActivity.EXTRA_QR_TEXT)
                    if (!payload.isNullOrBlank()) {
                        sendQrResult("onNativeQrScanResult", payload, null)
                    } else {
                        sendQrResult(
                            "onNativeQrScanResult",
                            null,
                            "The scanner returned an empty result. Try again."
                        )
                    }
                }
            }

            REQUEST_QR_IMAGE -> {
                val uri = if (resultCode == RESULT_OK) data?.data else null
                if (uri != null) {
                    decodeQrImage(uri)
                }
            }
        }
    }

    private fun decodeQrImage(uri: Uri) {
        val scannerOptions = BarcodeScannerOptions.Builder()
            .setBarcodeFormats(Barcode.FORMAT_QR_CODE)
            .build()
        val scanner: BarcodeScanner = BarcodeScanning.getClient(scannerOptions)

        try {
            val image = InputImage.fromFilePath(this, uri)
            scanner.process(image)
                .addOnSuccessListener { barcodes ->
                    val payload = barcodes.firstNotNullOfOrNull { barcode ->
                        barcode.rawValue?.takeIf { it.isNotBlank() }
                    }
                    if (payload == null) {
                        sendQrResult(
                            "onNativeQrImageResult",
                            null,
                            "No QR code was found in this image. Try a sharper image with the entire code visible."
                        )
                    } else {
                        sendQrResult("onNativeQrImageResult", payload, null)
                    }
                }
                .addOnFailureListener {
                    sendQrResult(
                        "onNativeQrImageResult",
                        null,
                        "The image could not be decoded. Try another image format or a clearer image."
                    )
                }
                .addOnCompleteListener {
                    scanner.close()
                }
        } catch (_: Exception) {
            scanner.close()
            sendQrResult(
                "onNativeQrImageResult",
                null,
                "The selected image could not be opened. Choose another image."
            )
        }
    }

    private fun sendQrResult(callbackName: String, payload: String?, error: String?) {
        if (!::web.isInitialized || isFinishing || isDestroyed) return
        val resultJson = if (payload != null) {
            "{ok:true,text:" + org.json.JSONObject.quote(payload) + "}"
        } else {
            "{ok:false,error:" + org.json.JSONObject.quote(
                error ?: "QR decoding failed."
            ) + "}"
        }
        runOnUiThread {
            if (::web.isInitialized && !isFinishing && !isDestroyed) {
                web.evaluateJavascript(
                    "window." + callbackName + " && window." + callbackName + "(" + resultJson + ");",
                    null
                )
            }
        }
    }

    override fun onPause() {
        if (::web.isInitialized) web.onPause()
        super.onPause()
    }

    override fun onResume() {
        super.onResume()
        if (::web.isInitialized) web.onResume()
    }

    override fun onDestroy() {
        fileCallback?.onReceiveValue(null)
        fileCallback = null
        if (::web.isInitialized) {
            web.stopLoading()
            web.webChromeClient = null
            web.removeAllViews()
            web.destroy()
        }
        super.onDestroy()
    }

    @Deprecated("Deprecated in Java")
    override fun onBackPressed() {
        if (::web.isInitialized && web.canGoBack()) {
            web.goBack()
        } else {
            super.onBackPressed()
        }
    }

    companion object {
        private const val REQUEST_WEB_FILE = 2
        private const val REQUEST_QR_CAMERA = 3
        private const val REQUEST_QR_IMAGE = 4
    }
}
