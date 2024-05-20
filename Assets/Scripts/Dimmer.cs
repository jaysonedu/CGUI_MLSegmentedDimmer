using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.MagicLeap;
using UnityEngine.UI;

public class Dimming : MonoBehaviour
{
    [SerializeField]
    private TMPro.TextMeshProUGUI _debugText;
    [SerializeField]
    private TMPro.TextMeshProUGUI _currentDimmer;
    private GameObject _target;
    private Renderer _dimmingRenderer;
    //[SerializeField]
    //private GameObject _plane;
    [SerializeField, Tooltip("Desired width for the camera capture")]
    private int _captureWidth = 1280;
    [SerializeField, Tooltip("Desired height for the camera capture")]
    private int _captureHeight = 720;

    [SerializeField]
    private GameObject cube;
    [SerializeField]
    private Renderer _cubeRenderer;
    
    
    
    [SerializeField]
    private GameObject sign;
    [SerializeField]
    private Renderer _signRenderer;
    [SerializeField]
    private GameObject Obj;
    [SerializeField]
    private Renderer _objectRenderer;
    

    [SerializeField]
    private Camera _mainCamera;

    private MLCamera _camera;
    private MLCamera.CaptureConfig _captureConfig;
    //The identifier can either target the Main or CV cameras.
    private MLCamera.Identifier _identifier = MLCamera.Identifier.Main;
    private MLCamera.ResultExtras _resultExtras;

    //Is true if the camera is ready to be connected.
    private bool _cameraDeviceAvailable;

    private Renderer _planeRenderer;
    private Texture2D _videoTextureRgb;
    private Texture2D _staggeredVideoTextureRgb;

    private float _waitTime = 0.1f;
    private float _timer = 0.0f;

    //The camera capture state
    bool _isCapturing;

    public const int PIXEL_PROCESSING_STRIDE_X = 10;
    public const int PIXEL_PROCESSING_STRIDE_Y = 10;

    private readonly MLPermissions.Callbacks permissionCallbacks = new MLPermissions.Callbacks();

    private void Awake()
    {
        permissionCallbacks.OnPermissionGranted += OnPermissionGranted;
        permissionCallbacks.OnPermissionDenied += OnPermissionDenied;
        permissionCallbacks.OnPermissionDeniedAndDontAskAgain += OnPermissionDenied;
    }

    void OnEnable()
    {
        //_debugText.text += String.Format("  Debug logs start\n");
        //_planeRenderer = _plane.GetComponent<Renderer>();
        CubeDimmer();
        MLPermissions.RequestPermission(MLPermission.Camera, permissionCallbacks);
        MLPermissions.RequestPermission(MLPermission.RecordAudio, permissionCallbacks);
        MLSegmentedDimmer.Activate();
        CheckDimmerMode();
        TryEnableMLCamera();
    }

    void OnDisable()
    {
        StopCapture();
    }

    private void TryEnableMLCamera()
    {
        if (!MLPermissions.CheckPermission(MLPermission.Camera).IsOk)
            return;

        StartCoroutine(EnableMLCamera());
    }

    public void CubeDimmer()
    {
        _target = cube;
        _dimmingRenderer = _cubeRenderer;
        _currentDimmer.text = "Cube dimmer is activated!\n";
    }

    
    public void SignDimmer()
    {
        _target = sign;
        _dimmingRenderer = _signRenderer;
        _currentDimmer.text = "Sign dimmer is activated!";
    }

    public void ObjectDimmer()
    {
        _target = Obj;
        _dimmingRenderer = _objectRenderer;
        _currentDimmer.text = "Donut dimmer is activated!";
    }
    

    public void DisableDimmer()
    {
        _target = null;
        _dimmingRenderer = null;
        _currentDimmer.text = "Segmented Dimmer is paused.\n";
    }

    void Update()
    {
        if (_isCapturing)
        {
            _timer += Time.deltaTime;

            // Check if we have reached beyond 100 ms.
            // Subtracting two is more accurate over time than resetting to zero.
            if (_timer > _waitTime)
            {
                if (InFrontOfCamera())
                {
                    Rect targetBoundingBox = GetBoundingBox();
                    if (_videoTextureRgb != null)
                    {
                        //_debugText.text += String.Format("  Texture2D Renderer width: {0} and height: {1}\n", _videoTextureRgb.width, _videoTextureRgb.height);

                        CopyTexture();

                        float calculatedAvgBrightness = ProcessPixels(targetBoundingBox);
                        DimObject(calculatedAvgBrightness);

                        //_debugText.text += String.Format("  Display Opacity of Dimming Material: {0}\n", _dimmingRenderer.material.GetFloat("_DimmingValue"));
                    }
                }

                // Remove the recorded 100 ms.
                _timer = _timer - _waitTime;
            }
        }
    }

    private void CopyTexture()
    {
        if (_staggeredVideoTextureRgb != null &&
            (_staggeredVideoTextureRgb.width != _videoTextureRgb.width || _staggeredVideoTextureRgb.height != _videoTextureRgb.height))
        {
            Destroy(_staggeredVideoTextureRgb);
            _staggeredVideoTextureRgb = null;
        }

        if (_staggeredVideoTextureRgb == null)
        {
            _staggeredVideoTextureRgb = new Texture2D(_videoTextureRgb.width, _videoTextureRgb.height, TextureFormat.RGBA32, false);
            _staggeredVideoTextureRgb.filterMode = FilterMode.Bilinear;
        }

        _staggeredVideoTextureRgb.SetPixels(_videoTextureRgb.GetPixels());
        _staggeredVideoTextureRgb.Apply();
    }

    private void DimObject(float threshold)
    {
        _dimmingRenderer.material.SetFloat("_DimmingValue", threshold);
    }

    private float ProcessPixels(Rect boundingBox)
    {

        int minX = (int)Mathf.Max(0, boundingBox.x);
        int maxX = (int)Mathf.Min(_staggeredVideoTextureRgb.width, boundingBox.x + boundingBox.width);
        int minY = (int)Mathf.Max(0, boundingBox.y);
        int maxY = (int)Mathf.Min(_staggeredVideoTextureRgb.height, boundingBox.y + boundingBox.height);


        // _debugText.text += string.Format("  minX = {0}, maxX = {1}, minY = {2}, maxY = {3}\n", minX, maxX, minY, maxY);

        float averageLuminance = 0;
        int numPixels = 0;
        for (int y = minY; y < maxY; y += PIXEL_PROCESSING_STRIDE_Y)
        {
            for (int x = minX; x < maxX; x += PIXEL_PROCESSING_STRIDE_X)
            {
                Color pixel = _staggeredVideoTextureRgb.GetPixel(y, x);

                float luminance = 0.2126f * pixel.r +
                                  0.7152f * pixel.g +
                                  0.0722f * pixel.b;

                averageLuminance += luminance;
                ++numPixels;
            }
        }
        averageLuminance /= numPixels;
        _debugText.text = String.Format("  Average luminance in bounding box: {0}\n", averageLuminance);

        return averageLuminance;
    }

    public bool InFrontOfCamera()
    {
        return Vector3.Dot(_mainCamera.transform.forward, _target.transform.position - _mainCamera.transform.position) > 0;
    }

    private Rect GetBoundingBox()
    {
        float scalingValue = 0.25f;

        BoxCollider boxCollider = _target.GetComponent<BoxCollider>();

        var extentPoints = new Vector2[]
        {
            _mainCamera.WorldToScreenPoint(_target.transform.TransformPoint(boxCollider.center + new Vector3(-boxCollider.size.x * scalingValue, -boxCollider.size.y * scalingValue, -boxCollider.size.z * scalingValue) * 0.5f)),
            _mainCamera.WorldToScreenPoint(transform.TransformPoint(boxCollider.center + new Vector3(boxCollider.size.x * scalingValue, -boxCollider.size.y * scalingValue, -boxCollider.size.z * scalingValue) * 0.5f)),
            _mainCamera.WorldToScreenPoint(transform.TransformPoint(boxCollider.center + new Vector3(boxCollider.size.x * scalingValue, -boxCollider.size.y * scalingValue, boxCollider.size.z * scalingValue) * 0.5f)),
            _mainCamera.WorldToScreenPoint(transform.TransformPoint(boxCollider.center + new Vector3(-boxCollider.size.x * scalingValue, -boxCollider.size.y * scalingValue, boxCollider.size.z * scalingValue) * 0.5f)),
            _mainCamera.WorldToScreenPoint(transform.TransformPoint(boxCollider.center + new Vector3(-boxCollider.size.x * scalingValue, boxCollider.size.y * scalingValue, -boxCollider.size.z * scalingValue) * 0.5f)),
            _mainCamera.WorldToScreenPoint(transform.TransformPoint(boxCollider.center + new Vector3(boxCollider.size.x * scalingValue, boxCollider.size.y * scalingValue, -boxCollider.size.z * scalingValue) * 0.5f)),
            _mainCamera.WorldToScreenPoint(transform.TransformPoint(boxCollider.center + new Vector3(boxCollider.size.x * scalingValue, boxCollider.size.y * scalingValue, boxCollider.size.z * scalingValue) * 0.5f)),
            _mainCamera.WorldToScreenPoint(transform.TransformPoint(boxCollider.center + new Vector3(-boxCollider.size.x * scalingValue, boxCollider.size.y * scalingValue, boxCollider.size.z * scalingValue) * 0.5f))
        };

        Vector2 lowerLeft = extentPoints[0];
        Vector2 upperRight = extentPoints[0];

        foreach (Vector2 v in extentPoints)
        {
            lowerLeft = Vector2.Min(lowerLeft, v);
            upperRight = Vector2.Max(upperRight, v);
        }

        float x = lowerLeft.x;
        float y = lowerLeft.y;
        float width = (upperRight.x - lowerLeft.x);
        float height = (upperRight.y - lowerLeft.y);

        // _debugText.text += String.Format("  Bounding box Rect: (x = {0}, y = {1}, width = {2}, height {3})\n", x, y, width, height); // -.4 -.4 1.8 1.8
        return new Rect(x, y, width, height);
    }

    // Convert to screen space of physical camera on Magic Leap 2
    private Vector2 WorldToScreenPoint(Vector3 worldPoint)
    {
        Vector3 transformPoint = Vector3.zero;

        MLResult result = MLCVCamera.GetFramePose(_resultExtras.VCamTimestamp, out Matrix4x4 outMatrix);
        if (result.IsOk)
        {
            transformPoint = outMatrix.MultiplyPoint3x4(worldPoint);

            string cameraExtrinsics = "Camera Extrinsics";
            cameraExtrinsics += "Position " + outMatrix.GetPosition(); // (x, y)
            cameraExtrinsics += "Rotation " + outMatrix.rotation; // (r1, r2, r3, 24)
            //_debugText.text += String.Format(cameraExtrinsics + "\n");
        }

        if (_resultExtras.Intrinsics != null)
        {
            float fx = _resultExtras.Intrinsics.Value.FocalLength.x;
            float fy = _resultExtras.Intrinsics.Value.FocalLength.y;
            float px = _resultExtras.Intrinsics.Value.PrincipalPoint.x;
            float py = _resultExtras.Intrinsics.Value.PrincipalPoint.y;

            Vector2 cameraIntPoint = new Vector3
                (
                    Vector3.Dot(new Vector3(fx, 0, px), transformPoint),
                    Vector3.Dot(new Vector3(0, fy, py), transformPoint),
                    Vector3.Dot(new Vector3(0, 0, 1), transformPoint)
                );
            transformPoint = cameraIntPoint;

            string cameraIntrinsics = "Camera Intrinsics";
            cameraIntrinsics += "\n Width " + _resultExtras.Intrinsics.Value.Width;
            cameraIntrinsics += "\n Height " + _resultExtras.Intrinsics.Value.Height;
            cameraIntrinsics += "\n FOV " + _resultExtras.Intrinsics.Value.FOV;
            cameraIntrinsics += "\n FocalLength " + _resultExtras.Intrinsics.Value.FocalLength; // (fx, fy)
            cameraIntrinsics += "\n PrincipalPoint " + _resultExtras.Intrinsics.Value.PrincipalPoint; // (px, py)
            //_debugText.text += String.Format(cameraIntrinsics + "\n");
        }

        return new Vector2(transformPoint.x, transformPoint.y);
    }



    //Waits for the camera to be ready and then connects to it.
    private IEnumerator EnableMLCamera()
    {
        //Checks the main camera's availability.
        while (!_cameraDeviceAvailable)
        {
            //_debugText.text += String.Format("  Looking for MLCamera\n");
            MLResult result = MLCamera.GetDeviceAvailabilityStatus(_identifier, out _cameraDeviceAvailable);
            if (result.IsOk == false || _cameraDeviceAvailable == false)
            {
                // Wait until camera device is available
                yield return new WaitForSeconds(1.0f);
            }
        }
        ConnectCamera();
    }

    private void ConnectCamera()
    {
        //Once the camera is available, we can connect to it.
        if (_cameraDeviceAvailable)
        {
            MLCamera.ConnectContext connectContext = MLCamera.ConnectContext.Create();
            connectContext.CamId = _identifier;
            //MLCamera.Identifier.Main is the only camera that can access the virtual and mixed reality flags
            connectContext.Flags = MLCamera.ConnectFlag.CamOnly;
            connectContext.EnableVideoStabilization = true;

            _camera = MLCamera.CreateAndConnect(connectContext);
            if (_camera != null)
            {
                //_debugText.text += String.Format("  Camera device connected\n");
                ConfigureCameraInput();
                SetCameraCallbacks();
            }
        }
    }

    private void ConfigureCameraInput()
    {
        //Gets the stream capabilities the selected camera. (Supported capture types, formats and resolutions)
        MLCamera.StreamCapability[] streamCapabilities = MLCamera.GetImageStreamCapabilitiesForCamera(_camera, MLCamera.CaptureType.Video);

        if (streamCapabilities.Length == 0)
            return;

        //Set the default capability stream
        MLCamera.StreamCapability defaultCapability = streamCapabilities[0];

        //Try to get the stream that most closely matches the target width and height
        if (MLCamera.TryGetBestFitStreamCapabilityFromCollection(streamCapabilities, _captureWidth, _captureHeight,
                MLCamera.CaptureType.Video, out MLCamera.StreamCapability selectedCapability))
        {
            defaultCapability = selectedCapability;
        }

        //Initialize a new capture config.
        _captureConfig = new MLCamera.CaptureConfig();
        //Set RGBA video as the output
        MLCamera.OutputFormat outputFormat = MLCamera.OutputFormat.RGBA_8888;
        //Set the Frame Rate to 30fps
        _captureConfig.CaptureFrameRate = MLCamera.CaptureFrameRate._30FPS;
        //Initialize a camera stream config.
        //The Main Camera can support up to two stream configurations
        _captureConfig.StreamConfigs = new MLCamera.CaptureStreamConfig[1];
        _captureConfig.StreamConfigs[0] = MLCamera.CaptureStreamConfig.Create(
            defaultCapability, outputFormat
        );
        StartVideoCapture();
    }

    private void StartVideoCapture()
    {
        MLResult result = _camera.PrepareCapture(_captureConfig, out MLCamera.Metadata metaData);
        if (result.IsOk)
        {
            //Trigger auto exposure and auto white balance
            _camera.PreCaptureAEAWB();
            //Starts video capture. This call can also be called asynchronously 
            //Images capture uses the CaptureImage function instead.
            result = _camera.CaptureVideoStart();
            _isCapturing = MLResult.DidNativeCallSucceed(result.Result, nameof(_camera.CaptureVideoStart));
            if (_isCapturing)
            {
                //_debugText.text += String.Format("  Video capture started!\n");
            }
            else
            {
                //_debugText.text += String.Format("  Could not start camera capture. Result : {0}\n", result);
            }
        }
    }

    private void StopCapture()
    {
        if (_isCapturing)
        {
            _camera.CaptureVideoStop();
        }

        _camera.Disconnect();
        _camera.OnRawVideoFrameAvailable -= RawVideoFrameAvailable;
        _isCapturing = false;
    }

    //Assumes that the capture configure was created with a Video CaptureType
    private void SetCameraCallbacks()
    {
        //Provides frames in either YUV/RGBA format depending on the stream configuration
        _camera.OnRawVideoFrameAvailable += RawVideoFrameAvailable;
    }

    void RawVideoFrameAvailable(MLCamera.CameraOutput output, MLCamera.ResultExtras extras, MLCameraBase.Metadata metadataHandle)
    {
        if (output.Format == MLCamera.OutputFormat.RGBA_8888)
        {
            //Flips the frame vertically so it does not appear upside down.
            MLCamera.FlipFrameVertically(ref output);
            UpdateRGBTexture(ref _videoTextureRgb, output.Planes[0]);
        }

        _resultExtras = extras;
    }

    private void UpdateRGBTexture(ref Texture2D videoTextureRGB, MLCamera.PlaneInfo imagePlane)
    {

        if (videoTextureRGB != null &&
            (videoTextureRGB.width != imagePlane.Width || videoTextureRGB.height != imagePlane.Height))
        {
            Destroy(videoTextureRGB);
            videoTextureRGB = null;
        }

        if (videoTextureRGB == null)
        {
            // Create a new texture that will display the RGB image
            videoTextureRGB = new Texture2D((int)imagePlane.Width, (int)imagePlane.Height, TextureFormat.RGBA32, false);
            videoTextureRGB.filterMode = FilterMode.Bilinear;
        }

        //debugText.text += String.Format("imagePlane.Width = {0}, imagePlane.Height = {1}\n", imagePlane.Width, imagePlane.Height);
        int actualWidth = (int)(imagePlane.Width * imagePlane.PixelStride);

        // Image width and stride may differ due to padding bytes for memory alignment. Skip over padding bytes when accessing pixel data.
        if (imagePlane.Stride != actualWidth)
        {
            // Create a new array to store the pixel data without padding
            var newTextureChannel = new byte[actualWidth * imagePlane.Height];
            // Loop through each row of the image
            for (int i = 0; i < imagePlane.Height; i++)
            {
                // Copy the pixel data from the original array to the new array, skipping the padding bytes
                Buffer.BlockCopy(imagePlane.Data, (int)(i * imagePlane.Stride), newTextureChannel, i * actualWidth, actualWidth);
            }
            // Load the new array as the texture data
            videoTextureRGB.LoadRawTextureData(newTextureChannel);
        }
        else // If the stride is equal to the width, no padding bytes are present
        {
            videoTextureRGB.LoadRawTextureData(imagePlane.Data);
        }
        videoTextureRGB.Apply();
        //debugText.text += String.Format("videoTextureRGB.width = {0}, videoTextureRGB.height = {1}\n", videoTextureRGB.width, videoTextureRGB.height);

        // Assign the Plane Texture to the resulting texture
        //_planeRenderer.material.mainTexture = videoTextureRGB;
    }

    private void OnPermissionDenied(string permission)
    {
        if (permission == MLPermission.Camera)
        {
            MLPluginLog.Error($"{permission} denied, example won't function.");
        }
        else if (permission == MLPermission.RecordAudio)
        {
            MLPluginLog.Error($"{permission} denied, audio wont be recorded in the file.");
        }

    }

    private void OnPermissionGranted(string permission)
    {
        MLPluginLog.Debug($"Granted {permission}.");
        TryEnableMLCamera();

    }

    public bool CheckDimmerMode()
    {
        // Get context
        using (var actClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            var context = actClass.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaClass systemGlobal = new AndroidJavaClass("android.provider.Settings$System");

            var dimmerMode = systemGlobal.CallStatic<int>("getInt", context.Call<AndroidJavaObject>("getContentResolver"), "is_segmented_dimmer_enabled");

            // _currentDimmer.text += String.Format("Dimmer Mode is set to : {0}",dimmerMode);

            return dimmerMode > 0;
        }
    }

}