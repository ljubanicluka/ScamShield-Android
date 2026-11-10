package rs.scamshield.app

import android.Manifest
import android.app.Activity
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.webkit.WebSettings
import android.widget.Toast
import android.os.Bundle
import android.webkit.PermissionRequest
import android.webkit.ValueCallback
import android.webkit.WebChromeClient
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebView
import android.webkit.WebViewClient
import android.webkit.JavascriptInterface
import java.util.concurrent.atomic.AtomicBoolean
import androidx.webkit.WebViewAssetLoader

/**
 * Hosts the ScamShield web app (assets/index.html) in a WebView.
 * - Served from https://appassets.androidplatform.net so the camera (a secure-context feature) works.
 * - Asks for the camera permission only when the page requests the camera.
 * - Lets the page's file inputs open the system picker (QR images, evidence, file hash).
 * - Back button: goes back inside the app first, then closes it. Links to other sites open in the browser.
 */
class MainActivity : Activity() {
    private val assetHost = "appassets.androidplatform.net"
    private lateinit var web: WebView
    private var pendingCamera: PermissionRequest? = null
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
                return if (uri.scheme == "https" && uri.host.equals(assetHost, ignoreCase = true) && uri.path?.startsWith("/assets/") == true) {
                    loader.shouldInterceptRequest(uri)
                } else {
                    // The UI is bundled locally. Remote data is fetched by the native advisory service only.
                    WebResourceResponse("text/plain", "utf-8", java.io.ByteArrayInputStream(ByteArray(0)))
                }
            }

            override fun shouldOverrideUrlLoading(view: WebView, request: WebResourceRequest): Boolean {
                val uri = request.url
                val scheme = uri.scheme?.lowercase() ?: return true
                if (scheme == "https" && uri.host.equals(assetHost, ignoreCase = true) && uri.path?.startsWith("/assets/") == true) return false
                if (scheme !in setOf("https", "http", "tel", "mailto")) return true
                return try {
                    startActivity(Intent(Intent.ACTION_VIEW, uri))
                    true
                } catch (_: Exception) {
                    Toast.makeText(this@MainActivity, "No app is available to open this link.", Toast.LENGTH_SHORT).show()
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
                    val result = try { advisoryService.refresh().toString() }
                    catch (_: Exception) { advisoryService.cachedSnapshot().toString() }
                    runOnUiThread {
                        feedRefreshRunning.set(false)
                        if (::web.isInitialized) {
                            val quoted = org.json.JSONObject.quote(result)
                            web.evaluateJavascript("window.onLiveAdvisories && window.onLiveAdvisories(JSON.parse($quoted));", null)
                        }
                    }
                }.start()
            }
        }, "ScamShieldNative")

        web.webChromeClient = object : WebChromeClient() {
            override fun onPermissionRequest(request: PermissionRequest) {
                runOnUiThread {
                    val trusted = request.origin.scheme == "https" &&
                        request.origin.host.equals(assetHost, ignoreCase = true)
                    val videoOnly = request.resources.size == 1 &&
                        request.resources[0] == PermissionRequest.RESOURCE_VIDEO_CAPTURE
                    if (!trusted || !videoOnly) {
                        request.deny()
                        return@runOnUiThread
                    }
                    if (checkSelfPermission(Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED) {
                        request.grant(arrayOf(PermissionRequest.RESOURCE_VIDEO_CAPTURE))
                    } else {
                        pendingCamera?.deny()
                        pendingCamera = request
                        requestPermissions(arrayOf(Manifest.permission.CAMERA), 1)
                    }
                }
            }

            override fun onShowFileChooser(
                view: WebView, callback: ValueCallback<Array<Uri>>, params: FileChooserParams
            ): Boolean {
                fileCallback?.onReceiveValue(null)
                fileCallback = callback
                return try {
                    startActivityForResult(params.createIntent(), 2)
                    true
                } catch (_: Exception) {
                    fileCallback = null
                    false
                }
            }
        }

        web.loadUrl("https://appassets.androidplatform.net/assets/index.html")
    }

    override fun onRequestPermissionsResult(code: Int, permissions: Array<out String>, results: IntArray) {
        super.onRequestPermissionsResult(code, permissions, results)
        if (code != 1) return
        val request = pendingCamera ?: return
        pendingCamera = null
        if (results.isNotEmpty() && results[0] == PackageManager.PERMISSION_GRANTED) {
            request.grant(arrayOf(PermissionRequest.RESOURCE_VIDEO_CAPTURE))
        } else {
            request.deny()
        }
    }

    @Deprecated("Deprecated in Java")
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        if (requestCode == 2) {
            val result = if (resultCode == RESULT_OK) WebChromeClient.FileChooserParams.parseResult(resultCode, data) else null
            fileCallback?.onReceiveValue(result)
            fileCallback = null
        }
    }

    override fun onPause() {
        if (::web.isInitialized) {
            web.evaluateJavascript("window.stopScamShieldCamera && window.stopScamShieldCamera();", null)
            web.onPause()
        }
        super.onPause()
    }

    override fun onResume() {
        super.onResume()
        if (::web.isInitialized) web.onResume()
    }

    override fun onDestroy() {
        pendingCamera?.deny()
        pendingCamera = null
        fileCallback?.onReceiveValue(null)
        fileCallback = null
        if (::web.isInitialized) {
            web.evaluateJavascript("window.stopScamShieldCamera && window.stopScamShieldCamera();", null)
            web.stopLoading()
            web.webChromeClient = null
            web.removeAllViews()
            web.destroy()
        }
        super.onDestroy()
    }

    @Deprecated("Deprecated in Java")
    override fun onBackPressed() {
        if (::web.isInitialized && web.canGoBack()) web.goBack() else super.onBackPressed()
    }
}
