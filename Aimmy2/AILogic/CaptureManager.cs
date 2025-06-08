using Aimmy2.Class;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Drawing;
using System.Drawing.Imaging;
using Device = SharpDX.Direct3D11.Device;

namespace AILogic
{
    internal class CaptureManager
    {
        #region Variables
        private const int IMAGE_SIZE = 640;
        public Bitmap? _screenCaptureBitmap { get; private set; }
        private Device _dxDevice;
        private OutputDuplication _deskDuplication;
        private Texture2DDescription _texDesc;
        private Texture2D _stagingTex;

        // Display change handling
        public readonly object _displayLock = new();
        public bool _displayChangesPending { get; set; } = false;
        #endregion
        /// <summary>
        /// Old function, leaving it here in-case my cleaned up version is causing issues - tay
        /// </summary>
        //public void InitializeDxgiDuplication()
        //{

        //    // Clean up any existing resources
        //    DisposeDxgiResources();
        //    try
        //    {
        //        using var factory = new Factory1();
        //        Output1? targetOutput1 = null;
        //        Adapter1? targetAdapter = null;
        //        int globalOutputIndex = 0;

        //        // Find the output by global index
        //        foreach (var adapter in factory.Adapters1)
        //        {
        //            var outputCount = adapter.GetOutputCount();
        //            for (int i = 0; i < outputCount; i++)
        //            {
        //                try
        //                {
        //                    using (var output = adapter.GetOutput(i))
        //                    {
        //                        var desc = output.Description;
        //                        var bounds = desc.DesktopBounds;

        //                        if (globalOutputIndex == DisplayManager.CurrentDisplayIndex)
        //                        {
        //                            targetOutput1 = output.QueryInterface<Output1>();
        //                            targetAdapter = adapter;
        //                            break;
        //                        }

        //                        globalOutputIndex++;
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    globalOutputIndex++;
        //                }
        //            }

        //            if (targetOutput1 != null) break;
        //        }

        //        if (targetOutput1 == null || targetAdapter == null)
        //        {
        //            targetAdapter = factory.Adapters1[0];
        //            targetOutput1 = targetAdapter.GetOutput(0).QueryInterface<Output1>();
        //        }

        //        // Create D3D11 device on the correct adapter
        //        _dxDevice = new Device(targetAdapter, DeviceCreationFlags.None);

        //        // Create desktop duplication
        //        _deskDuplication = targetOutput1.DuplicateOutput(_dxDevice);
        //        targetOutput1.Dispose();


        //        // Create staging texture
        //        _texDesc = new Texture2DDescription
        //        {
        //            CpuAccessFlags = CpuAccessFlags.Read,
        //            BindFlags = BindFlags.None,
        //            Format = Format.B8G8R8A8_UNorm,
        //            Height = IMAGE_SIZE,
        //            Width = IMAGE_SIZE,
        //            OptionFlags = ResourceOptionFlags.None,
        //            MipLevels = 1,
        //            ArraySize = 1,
        //            SampleDescription = new SampleDescription(1, 0),
        //            Usage = ResourceUsage.Staging
        //        };
        //        _stagingTex = new Texture2D(_dxDevice, _texDesc);

        //    }
        //    catch (Exception ex)
        //    {
        //        throw;
        //    }
        //}


        public void InitializeDxgiDuplication()
        {
            DisposeDxgiResources();

            try
            {
                using var factory = new Factory1();
                var (adapter, output) = FindTargetDisplay(factory);

                _dxDevice = new Device(adapter);
                _deskDuplication = output.DuplicateOutput(_dxDevice);

                // Create fixed-size staging texture
                _stagingTex = new Texture2D(_dxDevice, new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    BindFlags = BindFlags.None,
                    Format = Format.B8G8R8A8_UNorm,
                    Width = IMAGE_SIZE,
                    Height = IMAGE_SIZE,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Staging
                });
            }
            catch
            {
                DisposeDxgiResources();
                throw;
            }
        }

        private (Adapter1 adapter, Output1 output) FindTargetDisplay(Factory1 factory)
        {
            int index = 0;
            foreach (var adapter in factory.Adapters1)
            {
                for (int i = 0; i < adapter.GetOutputCount(); i++)
                {
                    try
                    {
                        using var output = adapter.GetOutput(i);
                        if (index++ == DisplayManager.CurrentDisplayIndex)
                        {
                            return (adapter, output.QueryInterface<Output1>());
                        }
                    }
                    catch { /* Skip unavailable outputs */ }
                }
            }

            // Fallback to primary display
            var primaryAdapter = factory.Adapters1[0];
            return (primaryAdapter, primaryAdapter.GetOutput(0).QueryInterface<Output1>());
        }
        public Bitmap? ScreenGrab(Rectangle detectionBox)
        {
            // Validate detection box before processing
            if (detectionBox.Width <= 0 || detectionBox.Height <= 0)
                return null;

            detectionBox.X = Math.Clamp(detectionBox.X, DisplayManager.ScreenLeft, DisplayManager.ScreenLeft + DisplayManager.ScreenWidth - IMAGE_SIZE);
            detectionBox.Y = Math.Clamp(detectionBox.Y, DisplayManager.ScreenTop, DisplayManager.ScreenTop + DisplayManager.ScreenHeight - IMAGE_SIZE);

            try
            {
                if (_deskDuplication.TryAcquireNextFrame(0, out _, out var desktopResource).Success && desktopResource != null)
                {
                    using (desktopResource)
                    {
                        return ProcessFrame(desktopResource, detectionBox);
                    }
                }
                return null;
            }
            catch (SharpDXException ex)
            {
                // Reinitialize on next iteration
                lock (_displayLock) { _displayChangesPending = true; }
                return null;
            }
            finally
            {
                try { _deskDuplication.ReleaseFrame(); }
                catch { /* ignore errors from release */ }
            }
        }
        private Bitmap? ProcessFrame(SharpDX.DXGI.Resource desktopResource, Rectangle detectionBox)
        {
            try
            {
                //using (desktopResource)
                var screenTexture2D = desktopResource.QueryInterface<Texture2D>();

                // Copy region - coordinates are relative to the display being captured
                var region = new ResourceRegion
                {
                    Left = detectionBox.Left - DisplayManager.ScreenLeft,
                    Top = detectionBox.Top - DisplayManager.ScreenTop,
                    Right = detectionBox.Right - DisplayManager.ScreenLeft,
                    Bottom = detectionBox.Bottom - DisplayManager.ScreenTop,
                    Front = 0,
                    Back = 1
                };

                _dxDevice.ImmediateContext.CopySubresourceRegion(
                    screenTexture2D, 0, region, _stagingTex, 0, 0, 0, 0);

                // Map and copy to bitmap
                var dataBox = _dxDevice.ImmediateContext.MapSubresource(
                    _stagingTex, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                try
                {
                    if (_screenCaptureBitmap == null || _screenCaptureBitmap.Width != detectionBox.Width ||  _screenCaptureBitmap.Height != detectionBox.Height)
                    {
                        _screenCaptureBitmap?.Dispose();
                        _screenCaptureBitmap = new Bitmap(detectionBox.Width, detectionBox.Height, PixelFormat.Format32bppArgb);
                    }

                    var bmpData = _screenCaptureBitmap.LockBits(
                        new Rectangle(0, 0, detectionBox.Width, detectionBox.Height),
                        ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                    Utilities.CopyMemory(bmpData.Scan0, dataBox.DataPointer,
                        detectionBox.Width * detectionBox.Height * 4);

                    _screenCaptureBitmap.UnlockBits(bmpData);
                    return _screenCaptureBitmap;
                }
                finally
                {
                    _dxDevice.ImmediateContext.UnmapSubresource(_stagingTex, 0);
                    screenTexture2D.Dispose();
                }
            }
            catch (Exception ex)
            {
                lock (_displayLock) { _displayChangesPending = true; }
                return null;
            }
        }


        public void DisposeDxgiResources()
        {
            try
            {
                try
                {
                    _deskDuplication?.ReleaseFrame();
                }
                catch (SharpDXException)
                {
                    // This is expected if no frame is currently acquired
                }

                _deskDuplication?.Dispose();
                _stagingTex?.Dispose();
                _dxDevice?.Dispose();

                _deskDuplication = null;
                _stagingTex = null;
                _dxDevice = null;

                // Small delay to ensure resources are fully released
                //Thread.Sleep(50);
            }
            catch (Exception ex)
            {
            }
        }
    }
}
