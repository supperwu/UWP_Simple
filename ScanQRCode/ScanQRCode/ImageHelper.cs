using System;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace ScanQRCode
{
    public static class ImageHelper
    {
        /// <summary>
        /// Asynchronously get the cropped stream.
        /// </summary>
        /// <param name="inputStream">The input stream</param>
        /// <param name="rect"></param>
        /// <returns></returns>
        public static async Task<IRandomAccessStream> GetCroppedStreamAsync(IRandomAccessStream inputStream, Rect rect)
        {
            if (inputStream == null)
                return null;

            var startPointX = (uint)Math.Floor(rect.X);
            var startPointY = (uint)Math.Floor(rect.Y);
            var width = (uint)Math.Floor(rect.Width);
            var height = (uint)Math.Floor(rect.Height);

            return await GetCroppedStreamAsync(inputStream, startPointX, startPointY, width, height);
        }

        /// <summary>
        /// Asynchronously get the cropped stream.
        /// </summary>
        /// <param name="inputStream">The input stream</param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static async Task<IRandomAccessStream> GetCroppedStreamAsync(IRandomAccessStream inputStream, uint x, uint y, uint width, uint height)
        {
            if (inputStream == null)
                return null;

            var pixelData = await GetCroppedPixelDataAsync(inputStream, x, y, width, height);
            if (pixelData == null)
                return null;

            var outputStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, width, height, 72, 72, pixelData);
            await encoder.FlushAsync();

            return outputStream;
        }

        /// <summary>
        /// Asynchronously get the cropped pixel data.
        /// </summary>
        /// <param name="inputStream">The input stream</param>
        /// <param name="rect"></param>
        /// <returns></returns>
        public static async Task<byte[]> GetCroppedPixelDataAsync(IRandomAccessStream inputStream, Rect rect)
        {
            if (inputStream == null)
                return null;

            var startPointX = (uint)Math.Floor(rect.X);
            var startPointY = (uint)Math.Floor(rect.Y);
            var width = (uint)Math.Floor(rect.Width);
            var height = (uint)Math.Floor(rect.Height);

            return await GetCroppedPixelDataAsync(inputStream, startPointX, startPointY, width, height);
        }

        /// <summary>
        /// Asynchronously get the cropped pixel data.
        /// </summary>
        /// <param name="inputStream">The input stream</param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static async Task<byte[]> GetCroppedPixelDataAsync(IRandomAccessStream inputStream, uint x, uint y, uint width, uint height)
        {
            if (inputStream == null)
                return null;

            var decoder = await BitmapDecoder.CreateAsync(inputStream);

            // Refine the start point
            if (x + width > decoder.PixelWidth)
                x = decoder.PixelWidth - width;
            if (y + height > decoder.PixelHeight)
                y = decoder.PixelHeight - height;

            var transform = new BitmapTransform()
            {
                Bounds = new BitmapBounds
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height
                },
                InterpolationMode = BitmapInterpolationMode.Fant,
                ScaledWidth = decoder.PixelWidth,
                ScaledHeight = decoder.PixelHeight
            };

            var pixelProvider = await decoder.GetPixelDataAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, transform,
                                                     ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);


            return pixelProvider.DetachPixelData();
        }
    }
}