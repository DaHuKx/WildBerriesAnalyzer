using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;

namespace WildBerriesAnalyzer.Mobile.Helpers
{
    /// <summary>
    /// Сильное даунскейлирование превью — визуально как размытие для 18+.
    /// </summary>
    public static class AdultImageEffects
    {
        public static byte[]? CreateBlurredPreview(byte[] sourceBytes, float maxSide = 14f)
        {
            if (sourceBytes is null || sourceBytes.Length == 0)
            {
                return null;
            }

            try
            {
                using var input = new MemoryStream(sourceBytes);
                var image = PlatformImage.FromStream(input);
                if (image is null)
                {
                    return null;
                }

                using (image)
                {
                    // Downsize возвращает новый IImage; исходный image остаётся у нас в using.
                    var small = image.Downsize(maxSide);
                    if (small is null)
                    {
                        return null;
                    }

                    using (small)
                    using (var output = new MemoryStream())
                    {
                        small.Save(output, ImageFormat.Png);
                        return output.ToArray();
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
