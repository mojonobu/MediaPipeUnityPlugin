using Mediapipe.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARCameraSource : ImageSource
{

  private GameObject _arSessionObject;
  private GameObject _arSessionOriginObject;
  private GameObject _arCameraObject;


  [SerializeField] private ARCameraManager _arCameraManager;
  private Texture2D _arCameraRawTexture;
  XRCpuImage.Transformation m_Transformation = XRCpuImage.Transformation.MirrorY;

  [Tooltip("For the default resolution, the one whose width is closest to this value will be chosen")]
  [SerializeField] private int _preferableDefaultWidth = 1280;

  private const string _TAG = nameof(ARCameraSource);

  [SerializeField]
  private ResolutionStruct[] _defaultAvailableResolutions = new ResolutionStruct[] {
      new ResolutionStruct(176, 144, 30),
      new ResolutionStruct(320, 240, 30),
      new ResolutionStruct(424, 240, 30),
      new ResolutionStruct(640, 480, 30),
      new ResolutionStruct(848, 480, 30),
      new ResolutionStruct(960, 540, 30),
      new ResolutionStruct(1280, 960, 30),
      new ResolutionStruct(1600, 896, 30),
      new ResolutionStruct(1920, 1080, 30),
   };


  private unsafe void OnCameraFrameReceived(ARCameraFrameEventArgs obj)
  {
    if (!_arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
    {
      return;
    }

    var format = TextureFormat.RGBA32;



    // Convert the image to format, flipping the image across the Y axis.
    // We can also get a sub rectangle, but we'll get the full image here.
    var conversionParams = new XRCpuImage.ConversionParams(image, format, m_Transformation);

    // Texture2D allows us write directly to the raw texture data
    // This allows us to do the conversion in-place without making any copies.
    var rawTextureData = _arCameraRawTexture.GetRawTextureData<byte>();
    try
    {
      image.Convert(conversionParams, new IntPtr(rawTextureData.GetUnsafePtr()), rawTextureData.Length);
    }
    finally
    {
      // We must dispose of the XRCpuImage after we're finished
      // with it to avoid leaking native resources.
      image.Dispose();
    }

    // Apply the updated texture data to our texture
    _arCameraRawTexture.Apply();

  }


  private static readonly object _PermissionLock = new object();
  private static bool _IsPermitted = false;

  public override int textureWidth => !isPrepared ? 0 : _arCameraRawTexture.width;
  public override int textureHeight => !isPrepared ? 0 : _arCameraRawTexture.height;

  //public override bool isVerticallyFlipped => isPrepared && _arCameraRawTexture.videoVerticallyMirrored;
  //public override bool isFrontFacing => isPrepared && (aRCameraManager is ARCameraManager valueOfWebCamDevice) && valueOfWebCamDevice.isFrontFacing;



  public override string sourceName => "ARCamera";

  public override string[] sourceCandidateNames => new string[] { "ARCamera"};

  public override ResolutionStruct[] availableResolutions => _defaultAvailableResolutions;

  public override bool isPrepared => _arCameraManager != null;

  public override bool isPlaying => _arCameraManager != null;
  private bool _isInitialized;

  public Texture2D ArCameraRawTexture { get => _arCameraRawTexture; set => _arCameraRawTexture = value; }
  public ARCameraManager ArCameraManager { get => _arCameraManager; set => _arCameraManager = value; }

  public override Texture GetCurrentTexture()
  {
    return _arCameraRawTexture;
  }

  public override void Pause()
  {
    _arSessionObject.SetActive(false);
    _arSessionOriginObject.SetActive(false);
  }

  public override IEnumerator Play()
  {   
    if(_arCameraManager== null)
    {
      InitializeARScene();
    }
    //OnFrameReceivedなど？
    _arCameraManager.frameReceived += OnCameraFrameReceived;


    using var configs = _arCameraManager.GetConfigurations(Allocator.Temp);

    var reso = new Vector2(640, 480);

    foreach (var c in configs)
    {
      if (c.resolution == reso)
      {
        Debug.Log($"Camera configuration set.:\n{c}");
        _arCameraManager.currentConfiguration = c;
        break;
      }
    }

    var format = TextureFormat.RGBA32;


    if (_arCameraRawTexture == null)
    {
      _arCameraRawTexture = new Texture2D((int)reso.x, (int)reso.y, format, false);
    }

    yield return null;
  }

  private void InitializeARScene()
  {
    _arSessionObject = new GameObject("ARSession");
    _arSessionOriginObject = new GameObject("ARSessionOrigin");
    _arCameraObject = new GameObject("AR Camera");

    _ = _arSessionObject.AddComponent<ARSession>();
    _ = _arSessionOriginObject.AddComponent<ARSessionOrigin>();
    _ = _arCameraObject.AddComponent<Camera>();
    _arCameraObject.transform.SetParent(_arSessionOriginObject.transform);

    // ARCameraManagerとARPoseDriverを追加
    _ = _arCameraObject.AddComponent<ARCameraManager>();
    _ = _arCameraObject.AddComponent<ARPoseDriver>();

    // ARInputManagerを追加
    _ = _arSessionObject.AddComponent<ARInputManager>();

    // ARCameraBackgroundを追加
    _ = _arCameraObject.AddComponent<ARCameraBackground>();

    _arSessionObject.SetActive(false);
    _arSessionOriginObject.SetActive(false);
  }
  public void StartAR()
  {
    _ = StartCoroutine(ActivateAfterOneFrame());
  }
  private IEnumerator ActivateAfterOneFrame()
  {
    yield return new WaitForEndOfFrame();

    _arSessionObject.SetActive(true);
    _arSessionOriginObject.SetActive(true);
  }

  public override IEnumerator Resume()
  {
    yield return null;

  }

  public override void SelectSource(int sourceId)
  {
    return;
  }

  public override void Stop()
  {
    _arSessionObject.SetActive(false);
    _arSessionOriginObject.SetActive(false);
  }

}
