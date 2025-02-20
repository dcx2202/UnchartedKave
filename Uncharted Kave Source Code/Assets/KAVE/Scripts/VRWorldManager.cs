﻿/*
If you use or adapt this software in your research please consult
the author at afonso.goncalves@m-iti.org on how to cite it.

Copyright (C) 2017  Afonso Gonçalves 

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>
 */

using UnityEngine;

namespace VRKave
{
    public class VRWorldManager : MonoBehaviour
    {
        public GameObject KinectSensorPrefab;
        public GameObject ArtTrackSensorPrefab;
        public Camera ProjectorPrefab;
        public GameObject SurfacePrefab;
        public Camera UserViewCameraPrefab;
        public GameObject ScreenPrefab;

        private VRLoadCalibration _configuration = new VRLoadCalibration();

        private GameObject _sensor;
        private Camera[] _projectors;
        private GameObject[] _surfaces;
        private GameObject[] _screens;
        private GameObject[,] _surfaceEdges;
        private GameObject[,] _screenEdges;
        private Camera[] _userProjectorViewCameras;
        private Camera[] _userScreenViewCameras;
        private RenderTexture[] _surfaceTextures;

        private int _caveLayer = 31;

        private GameObject _head;
        private int _numberScreens;
        private int _numberSurfaces;
        private int _textureWidth = 2800;
        private int _textureHeight = 1050;
        public float KaveScale = 1; //Scale of the real world KAVE units used in calibration relative to the Unity project units. Ex: A KAVE with 2 meter tall wall and a scale of 3 will have walls of 2*3 Unity units tall when instantiated.

        void Start()
        {
            Load();
            InstantiateVR();
            _head = _sensor.transform.Find("Body").transform.Find("Head").gameObject;

            _surfaceEdges = new GameObject[_numberSurfaces, 4];
            for (int surface = 0; surface < _numberSurfaces; surface++)
                for (int corner = 0; corner < 4; corner++)
                    _surfaceEdges[surface, corner] = _surfaces[surface].transform.GetChild(corner).gameObject;

            _screenEdges = new GameObject[_numberScreens, 4];
            for (int screen = 0; screen < _numberScreens; screen++)
                for (int corner = 0; corner < 4; corner++)
                    _screenEdges[screen, corner] = _screens[screen].transform.GetChild(corner).gameObject;

            int numberOfDisplays = Display.displays.Length < _projectors.Length + _screens.Length ? Display.displays.Length : _projectors.Length + _screens.Length;
            for (int i = 1; i < numberOfDisplays; i++)
                Display.displays[i].Activate();
        }

        private void InstantiateVR()
        {
            _numberScreens = _configuration.Screens.Length;
            _numberSurfaces = _configuration.Surfaces.Length;

            //Instatiate the Sensor:
            switch (_configuration.Sensors.Type)
            {
                case VRLoadCalibration.SensorType.Kinect:
                    _sensor = Instantiate(KinectSensorPrefab, transform);
                    break;
                case VRLoadCalibration.SensorType.ArtTrack:
                    _sensor = Instantiate(ArtTrackSensorPrefab, transform);
                    _sensor.GetComponent<ArtTrack>().BodyID = _configuration.Sensors.BodyID;
                    break;
            }
            _sensor.transform.localPosition = new Vector3(_configuration.Sensors.Position.x, _configuration.Sensors.Position.y, _configuration.Sensors.Position.z);
            _sensor.transform.localRotation = Quaternion.Euler(_configuration.Sensors.Rotation.x, _configuration.Sensors.Rotation.y, _configuration.Sensors.Rotation.z);

            _sensor.gameObject.layer = _caveLayer;

            //Instantiate the Surfaces:
            int index = 0;
            _surfaces = new GameObject[_configuration.Surfaces.Length];
            _surfaceTextures = new RenderTexture[_configuration.Surfaces.Length];
            foreach (var surface in _configuration.Surfaces)
            {
                _surfaces[index] = Instantiate(SurfacePrefab, transform);
                _surfaces[index].transform.localPosition = new Vector3(surface.Position.x, surface.Position.y, surface.Position.z);
                _surfaces[index].transform.localRotation = Quaternion.Euler(surface.Rotation.x, surface.Rotation.y, surface.Rotation.z);
                _surfaces[index].transform.Rotate(new Vector3(0, 180, 0));
                _surfaces[index].transform.localScale = new Vector3(surface.Size.x / 10, surface.Size.y / 10, surface.Size.z / 10);
                _surfaces[index].layer = _caveLayer;
                _surfaceTextures[index] = new RenderTexture(_textureWidth, _textureHeight, 24, RenderTextureFormat.ARGB32);
                _surfaceTextures[index].antiAliasing = 2;
                _surfaceTextures[index].Create();
                index++;
            }

            //Instantiate the CAVE projectors:
            index = 0;
            _projectors = new Camera[_configuration.Projectors.Length];
            foreach (var projector in _configuration.Projectors)
            {
                _projectors[index] = Instantiate(ProjectorPrefab, transform);
                _projectors[index].transform.localPosition = new Vector3(projector.Position.x, projector.Position.y, projector.Position.z);
                _projectors[index].transform.localRotation = Quaternion.Euler(projector.Rotation.x, projector.Rotation.y, projector.Rotation.z);
#if !UNITY_EDITOR
                    _projectors[index].aspect = (float)Display.displays[projector.Display - 1].renderingWidth / Display.displays[projector.Display - 1].renderingHeight;
#endif
                _projectors[index].targetDisplay = projector.Display - 1;
                _projectors[index].farClipPlane = (_projectors[index].transform.position - _surfaces[_projectors[index].targetDisplay].transform.position).magnitude * KaveScale * 2;
                _projectors[index].fieldOfView = projector.FOV;
                SetObliqueness(0, projector.Fy, _projectors[index]);
                _projectors[index].gameObject.layer = _caveLayer;
                //_projectors[index].cullingMask = 1 << (_caveLayer - projector.Display);
                //_projectors[index].cullingMask |= 1 << _caveLayer;
                _projectors[index].gameObject.GetComponent<QuadWarp>()._tex.Clear();
                var surfaceIndex = 0;
                foreach (var surface in _configuration.Surfaces)
                {
                    if (surface.Display == projector.Display)
                    {
                        _projectors[index].gameObject.GetComponent<QuadWarp>()._tex.Add(_surfaceTextures[surfaceIndex]);
                        _projectors[index].gameObject.GetComponent<QuadWarp>()._vertices.Add(surface.Vertices);
                        _projectors[index].gameObject.GetComponent<QuadWarp>().DisplayIndex =
                            _projectors[index].targetDisplay;
                    }
                    surfaceIndex++;
                }
                index++;
            }

            //Instantiate the screens:
            index = 0;
            _screens = new GameObject[_configuration.Screens.Length];
            foreach (var screen in _configuration.Screens)
            {
                _screens[index] = Instantiate(ScreenPrefab, transform);
                _screens[index].transform.localPosition = new Vector3(screen.Position.x, screen.Position.y, screen.Position.z);
                _screens[index].transform.localRotation = Quaternion.Euler(screen.Rotation.x, screen.Rotation.y, screen.Rotation.z);
                _screens[index].transform.localScale = new Vector3(screen.Size.x, screen.Size.y, screen.Size.z);
                index++;
            }

            //Instantiate the user view cameras (cameras attached to the user head in the virtual world) for the projectors:
            _userProjectorViewCameras = new Camera[_configuration.Surfaces.Length];
            for (int i = 0; i < _configuration.Surfaces.Length; i++)
            {
                index = i;
                _userProjectorViewCameras[index] = Instantiate(UserViewCameraPrefab, transform.position, _surfaces[index].transform.rotation * Quaternion.Euler(90, 180, 0), _surfaces[index].transform);
                _userProjectorViewCameras[index].aspect = _surfaces[index].transform.localScale.x / _surfaces[index].transform.localScale.z;
                _userProjectorViewCameras[index].gameObject.layer = _caveLayer;
                _userProjectorViewCameras[index].targetTexture = _surfaceTextures[index];
                //_userProjectorViewCameras[index].cullingMask = 1 << ();      //The user is set to only see the default layer. Change this culling mask if you want the camera to see different layers (like water).
            }

            //Instantiate the user view cameras (cameras attached to the user head in the virtual world) for the screens:
            _userScreenViewCameras = new Camera[_configuration.Screens.Length];
            for (int i = 0; i < _configuration.Screens.Length; i++)
            {
                index = i;
                _userScreenViewCameras[index] = Instantiate(UserViewCameraPrefab, transform.position, _screens[index].transform.rotation, _screens[index].transform);
#if !UNITY_EDITOR
                    _userScreenViewCameras[index].aspect = _screens[index].transform.localScale.x / _screens[index].transform.localScale.y;
#endif
                _userScreenViewCameras[index].gameObject.layer = _caveLayer;
                //_userScreenViewCameras[index].cullingMask = 1 << ();      //The user is set to only see the default layer. Change this culling mask if you want the camera to see different layers (like water).
                _userScreenViewCameras[index].targetDisplay = _configuration.Screens[index].Display - 1;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                //Quit();
            }

            SetScale();
            SetHead();
        }

        private void SetScale()
        {
            gameObject.transform.localScale = new Vector3(KaveScale, KaveScale, KaveScale);
        }

        private void SetHead()
        {
            //Set the position
            SetHeadPosition();

            //Set the FOV & Orientation
            for (int cameraIndex = 0; cameraIndex < _numberSurfaces; cameraIndex++)
                SetHeadFovAndOrientationProjector(cameraIndex, _userProjectorViewCameras, _surfaceEdges);

            for (int cameraIndex = 0; cameraIndex < _numberScreens; cameraIndex++)
                SetHeadFovAndOrientationScreen(cameraIndex, _userScreenViewCameras, _screenEdges);
        }

        private void SetHeadFovAndOrientationProjector(int index, Camera[] cameras, GameObject[,] edges)
        {
            var bottomToTop = edges[index, 0].transform.position - edges[index, 2].transform.position;
            var leftToRight = edges[index, 1].transform.position - edges[index, 3].transform.position;

            //Set FOV
            cameras[index].ResetProjectionMatrix();
            cameras[index].fieldOfView = 2 * Mathf.Rad2Deg *
                                                  Mathf.Atan(bottomToTop.magnitude / 2 /
                                                             (cameras[index].transform.localPosition.y *
                                                              cameras[index].transform.parent.lossyScale.y));

            //Set the orientation
            float obV = cameras[index].transform.localPosition.z *
                        cameras[index].transform.parent.lossyScale.z / (bottomToTop.magnitude / 2);
            float obH = cameras[index].transform.localPosition.x *
                        cameras[index].transform.parent.lossyScale.x / (leftToRight.magnitude / 2);
            SetObliqueness(obH, obV, cameras[index]);
        }

        private void SetHeadFovAndOrientationScreen(int index, Camera[] cameras, GameObject[,] edges)
        {
            var bottomToTop = edges[index, 0].transform.position - edges[index, 2].transform.position;
            var leftToRight = edges[index, 1].transform.position - edges[index, 3].transform.position;

            //Set FOV
            cameras[index].ResetProjectionMatrix();
            cameras[index].fieldOfView = -2 * Mathf.Rad2Deg *
                                                  Mathf.Atan(bottomToTop.magnitude / 2 /
                                                             (cameras[index].transform.localPosition.z *
                                                              cameras[index].transform.parent.lossyScale.z));

            //Set the orientation
            float obV = cameras[index].transform.localPosition.y *
                        cameras[index].transform.parent.lossyScale.y / (bottomToTop.magnitude / 2);
            float obH = cameras[index].transform.localPosition.x *
                        cameras[index].transform.parent.lossyScale.x / (leftToRight.magnitude / 2);
            SetObliqueness(-obH, -obV, cameras[index]);
        }

        private void SetHeadPosition()
        {
            foreach (var userCamera in _userProjectorViewCameras)
                userCamera.transform.position = _head.transform.position;

            foreach (var userCamera in _userScreenViewCameras)
                userCamera.transform.position = _head.transform.position;
        }

        private bool Load()
        {
            var path = Application.dataPath + "/StreamingAssets/" + "calibration.xml";
            _configuration.LoadConfiguration(path);
            return true;
        }

        private void Quit()
        {
            Application.Quit();
        }

        void SetObliqueness(float horizObl, float vertObl, Camera cam)
        {
            Matrix4x4 mat = cam.projectionMatrix;
            mat[0, 2] = horizObl;
            mat[1, 2] = vertObl;
            cam.projectionMatrix = mat;
        }
    }
}
