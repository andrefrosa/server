﻿#region

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ionic.Zlib;
using UlteriusServer.Api.Network.Messages;
using UlteriusServer.Utilities.Extensions;
using UlteriusServer.WebCams;
using UlteriusServer.WebSocketAPI.Authentication;
using vtortola.WebSockets;

#endregion

namespace UlteriusServer.Api.Network.PacketHandlers
{
    public class WebCamPacketHandler : PacketHandler
    {
        private MessageBuilder _builder;
        private AuthClient _authClient;
        private Packet _packet;
        private WebSocket _client;



        /// <summary>
        /// Sends a list of all the currently plugged in cameras
        /// </summary>
        public void GetCameras()
        {
            WebCamManager.LoadWebcams();
            var cameras = WebCamManager.GetCameras();
            var data = new
            {
                cameraInfo = cameras
            };
            _builder.WriteMessage(data);
        }


        /// <summary>
        /// Starts a camera by its ID
        /// </summary>
        public void StartCamera()
        {
            var cameraId = _packet.Args[0].ToString();
            try
            {
                var cameraStarted = WebCamManager.StartCamera(cameraId);
                var camera = WebCamManager.Cameras[cameraId];
                var data = new
                {
                    cameraId,
                    cameraRunning = camera.IsRunning,
                    cameraStarted
                };
                _builder.WriteMessage(data);
            }
            catch (Exception e)
            {
                var data = new
                {
                    cameraId,
                    cameraRunning = false,
                    cameraStarted = false,
                    message = e.Message
                };
                _builder.WriteMessage(data);
            }
        }

        /// <summary>
        /// Stops a camera by its ID
        /// </summary>
        public void StopCamera()
        {
            var cameraId = _packet.Args[0].ToString();
            try
            {
                var cameraStopped = WebCamManager.StopCamera(cameraId);
                var camera = WebCamManager.Cameras[cameraId];
                var data = new
                {
                    cameraId,
                    cameraRunning = camera.IsRunning,
                    cameraStopped
                };
                _builder.WriteMessage(data);
            }
            catch (Exception e)

            {
                var data = new
                {
                    cameraId,
                    cameraRunning = false,
                    cameraStarted = false,
                    message = e.Message
                };
                _builder.WriteMessage(data);
            }
        }

        /// <summary>
        /// Pauses a camera by its ID, however this is not currently used.
        /// </summary>
        public void PauseCamera()
        {
            var cameraId = _packet.Args[0].ToString();
            var cameraPaused = WebCamManager.PauseCamera(cameraId);
            var camera = WebCamManager.Cameras[cameraId];
            var data = new
            {
                cameraRunning = camera.IsRunning,
                cameraPaused
            };
            _builder.WriteMessage(data);
        }


        /// <summary>
        /// Starts a camera stream, once this is called camera frames will be automatically sent to the client at a fixed rate. 
        /// This should be called after StartCamera
        /// </summary>
        public void StartStream()
        {
            var cameraId = _packet.Args[0].ToString();
            try
            {
                var cameraStream = new Task(() => GetWebCamFrame(cameraId));
                WebCamManager.Streams[cameraId] = cameraStream;
                WebCamManager.Streams[cameraId].Start();

                var data = new
                {
                    cameraId,
                    cameraStreamStarted = true
                };
                _builder.WriteMessage(data);
                Console.WriteLine("stream started for " + cameraId);
            }
            catch (Exception exception)
            {
                var data = new
                {
                    cameraId,
                    cameraStreamStarted = false,
                    message = exception.Message
                };

                _builder.WriteMessage(data);
            }
        }


        /// <summary>
        /// Stops a camera stream, this should be called after stopping the physical camera using StopCamera.
        /// </summary>
        public void StopStream()
        {
            var cameraId = _packet.Args[0].ToString();

            try
            {
                var streamThread = WebCamManager.Streams[cameraId];
                if (streamThread != null && !streamThread.IsCanceled && !streamThread.IsCompleted &&
                    streamThread.Status == TaskStatus.Running)
                {
                    streamThread.TryDispose();
                    WebCamManager.Frames.Clear();
                    if (_client.IsConnected)
                    {
                        var data = new
                        {
                            cameraId,
                            cameraStreamStopped = true
                        };
                        _builder.WriteMessage(data);
                    }
                }
            }
            catch (Exception e)
            {
                if (_client.IsConnected)
                {
                    var data = new
                    {
                        cameraId,
                        cameraStreamStopped = false,
                        message = e.Message
                    };
                    _builder.WriteMessage(data);
                }
            }
        }


        /// <summary>
        /// This grabs the latest frame from the camera and pushes it to the client as a loop
        /// </summary>
        public void GetWebCamFrame(string cameraId)
        {
            var camera = WebCamManager.Cameras[cameraId];
  
            while (_client != null && _client.IsConnected && camera != null && camera.IsRunning)
            {
                try

                {
                

                    var imageBytes = WebCamManager.Frames[cameraId];
                    if (imageBytes.Length > 0)
                    {
                       
                          
                            //JSON.net turns my byte array into base64.
                            var cameraData = new
                            {
                                cameraId,
                                cameraData = imageBytes.Select(b => (int)b).ToArray()
                            };
                            _builder.Endpoint = "cameraframe";
                            _builder.WriteMessage(cameraData);
                            Thread.Sleep(100);
                        
                    }
                }

                catch (Exception e)
                {
                    var data = new
                    {
                        cameraFrameFailed = true,
                        cameraId,
                        message = "Something went wrong and we were unable to get a frame from this camera!",
                        exceptionMessage = e.Message
                    };
                    _builder.WriteMessage(data);
                    Thread.Sleep(2500);
                }
            }
        }

        public override void HandlePacket(Packet packet)
        {
            _client = packet.Client;
            _authClient = packet.AuthClient;
            _packet = packet;
            _builder = new MessageBuilder(_authClient, _client, _packet.EndPointName, _packet.SyncKey);
            switch (_packet.EndPoint)
            {
                case PacketManager.EndPoints.StartCamera:
                    StartCamera();
                    break;
                case PacketManager.EndPoints.StopCamera:
                    StopCamera();
                    break;
                case PacketManager.EndPoints.PauseCamera:
                    PauseCamera();
                    break;
                case PacketManager.EndPoints.StopCameraStream:
                    StopStream();
                    break;
                case PacketManager.EndPoints.StartCameraStream:
                    StartStream();
                    break;
                case PacketManager.EndPoints.GetCameras:
                    GetCameras();
                    break;
                case PacketManager.EndPoints.GetCameraFrame:

                    break;
            }
        }

        public class Cameras
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string DisplayName { get; set; }
            public string DevicePath { get; set; }
        }
    }
}