package rs.scamshield.app

import android.Manifest
import android.annotation.SuppressLint
import android.app.Activity
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Color
import android.os.Bundle
import android.util.Size
import android.view.Gravity
import android.view.ViewGroup
import android.widget.Button
import android.widget.FrameLayout
import android.widget.LinearLayout
import android.widget.TextView
import androidx.activity.ComponentActivity
import androidx.activity.result.contract.ActivityResultContracts
import androidx.camera.core.Camera
import androidx.camera.core.CameraSelector
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.core.content.ContextCompat
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.barcode.BarcodeScanner
import com.google.mlkit.vision.barcode.BarcodeScannerOptions
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.common.InputImage
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Full-screen, native QR reader. The bundled ML Kit model means decoding works offline
 * immediately after installation; it does not depend on WebView camera support.
 */
class QrScannerActivity : ComponentActivity() {
    private lateinit var previewView: PreviewView
    private lateinit var statusView: TextView
    private lateinit var torchButton: Button
    private lateinit var cameraExecutor: ExecutorService
    private lateinit var scanner: BarcodeScanner
    private var cameraProvider: ProcessCameraProvider? = null
    private var activeCamera: Camera? = null
    private val resultSent = AtomicBoolean(false)
    private var torchEnabled = false

    private val cameraPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        if (granted) {
            startCamera()
        } else {
            setResult(
                Activity.RESULT_CANCELED,
                Intent().putExtra(
                    EXTRA_QR_ERROR,
                    "Camera permission was denied. Return to ScamShield and choose a QR image from the gallery."
                )
            )
            finish()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        cameraExecutor = Executors.newSingleThreadExecutor()
        val options = BarcodeScannerOptions.Builder()
            .setBarcodeFormats(Barcode.FORMAT_QR_CODE)
            .build()
        scanner = BarcodeScanning.getClient(options)

        setContentView(createContentView())
        window.addFlags(android.view.WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)

        if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA) ==
            PackageManager.PERMISSION_GRANTED
        ) {
            startCamera()
        } else {
            requestPermissions(arrayOf(Manifest.permission.CAMERA), REQUEST_CAMERA_PERMISSION)
        }
    }

    private fun createContentView(): FrameLayout {
        previewView = PreviewView(this).apply {
            layoutParams = FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT
            )
            scaleType = PreviewView.ScaleType.FILL_CENTER
            implementationMode = PreviewView.ImplementationMode.COMPATIBLE
            setBackgroundColor(Color.BLACK)
        }

        statusView = TextView(this).apply {
            text = "Point the camera at a QR code. The code is decoded on this device."
            setTextColor(Color.WHITE)
            textSize = 15f
            setPadding(18, 12, 18, 12)
            setBackgroundColor(Color.argb(210, 18, 28, 46))
            gravity = Gravity.CENTER
        }

        torchButton = Button(this).apply {
            text = "Flashlight"
            setOnClickListener { toggleTorch() }
        }
        val cancelButton = Button(this).apply {
            text = "Cancel"
            setOnClickListener { setResult(Activity.RESULT_CANCELED); finish() }
        }

        val controls = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(14, 12, 14, 18)
            setBackgroundColor(Color.argb(190, 12, 18, 28))
            addView(statusView, LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.WRAP_CONTENT
            ))
            val buttons = LinearLayout(this@QrScannerActivity).apply {
                orientation = LinearLayout.HORIZONTAL
                gravity = Gravity.CENTER
                addView(torchButton, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f))
                addView(cancelButton, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f))
            }
            addView(buttons, LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.WRAP_CONTENT
            ))
        }

        return FrameLayout(this).apply {
            addView(previewView)
            addView(controls, FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.WRAP_CONTENT,
                Gravity.BOTTOM
            ))
        }
    }

    @SuppressLint("MissingPermission")
    private fun startCamera() {
        statusView.text = "Starting camera…"
        val providerFuture = ProcessCameraProvider.getInstance(this)
        providerFuture.addListener({
            try {
                val provider = providerFuture.get()
                cameraProvider = provider

                val preview = Preview.Builder().build().also {
                    it.setSurfaceProvider(previewView.surfaceProvider)
                }

                val analysis = ImageAnalysis.Builder()
                    .setTargetResolution(Size(1280, 720))
                    .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                    .build()

                analysis.setAnalyzer(cameraExecutor) { imageProxy ->
                    analyzeFrame(imageProxy)
                }

                provider.unbindAll()
                activeCamera = provider.bindToLifecycle(
                    this,
                    CameraSelector.DEFAULT_BACK_CAMERA,
                    preview,
                    analysis
                )
                torchButton.isEnabled = activeCamera?.cameraInfo?.hasFlashUnit() == true
                if (!torchButton.isEnabled) torchButton.text = "No flashlight"
                statusView.text = "Hold the full QR code inside the camera view."
            } catch (_: Exception) {
                statusView.text = "Camera could not start. Close this screen and try again."
            }
        }, ContextCompat.getMainExecutor(this))
    }

    private fun analyzeFrame(imageProxy: androidx.camera.core.ImageProxy) {
        if (resultSent.get()) {
            imageProxy.close()
            return
        }

        val mediaImage = imageProxy.image
        if (mediaImage == null) {
            imageProxy.close()
            return
        }

        val image = try {
            InputImage.fromMediaImage(
                mediaImage,
                imageProxy.imageInfo.rotationDegrees
            )
        } catch (_: Exception) {
            imageProxy.close()
            return
        }

        val scanTask = try {
            scanner.process(image)
        } catch (_: Exception) {
            imageProxy.close()
            return
        }
        scanTask
            .addOnSuccessListener { barcodes ->
                val payload = barcodes.firstNotNullOfOrNull { barcode ->
                    barcode.rawValue?.takeIf { it.isNotBlank() }
                }
                if (payload != null && resultSent.compareAndSet(false, true)) {
                    runOnUiThread {
                        statusView.text = "QR code found."
                        setResult(
                            Activity.RESULT_OK,
                            Intent().putExtra(EXTRA_QR_TEXT, payload)
                        )
                        finish()
                    }
                }
            }
            .addOnFailureListener {
                // Keep scanning. A single blurry frame should not stop the scanner.
            }
            .addOnCompleteListener {
                imageProxy.close()
            }
    }

    private fun toggleTorch() {
        val camera = activeCamera ?: return
        if (!camera.cameraInfo.hasFlashUnit()) return
        torchEnabled = !torchEnabled
        camera.cameraControl.enableTorch(torchEnabled)
        torchButton.text = if (torchEnabled) "Flashlight: on" else "Flashlight: off"
    }


    override fun onDestroy() {
        cameraProvider?.unbindAll()
        if (::scanner.isInitialized) scanner.close()
        if (::cameraExecutor.isInitialized) cameraExecutor.shutdown()
        window.clearFlags(android.view.WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        super.onDestroy()
    }

    companion object {
        const val EXTRA_QR_TEXT = "rs.scamshield.app.QR_TEXT"
        const val EXTRA_QR_ERROR = "rs.scamshield.app.QR_ERROR"
    }
}
