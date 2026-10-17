using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using DesktopAutomationTest.Core;

namespace DesktopAutomationTest.Core;

/// <summary>
/// Provides helper methods for working with screenshots in UI tests.
/// </summary>
public static class ScreenshotHelper
{
    /// <summary>
    /// Compares two screenshots and returns the percentage difference.
    /// </summary>
    /// <param name="baselinePath">Path to the baseline screenshot.</param>
    /// <param name="actualPath">Path to the actual screenshot.</param>
    /// <param name="diffOutputPath">Optional path to save the difference image.</param>
    /// <returns>Percentage difference between the screenshots (0.0 = identical, 100.0 = completely different).</returns>
    public static double CompareScreenshots(string baselinePath, string actualPath, string? diffOutputPath = null)
    {
        Guard.AgainstNullOrEmpty(baselinePath, nameof(baselinePath));
        Guard.AgainstNullOrEmpty(actualPath, nameof(actualPath));

        if (!File.Exists(baselinePath))
            throw new FileNotFoundException("Baseline screenshot not found", baselinePath);

        if (!File.Exists(actualPath))
            throw new FileNotFoundException("Actual screenshot not found", actualPath);

        try
        {
            using var baselineImage = Image.FromFile(baselinePath);
            using var actualImage = Image.FromFile(actualPath);

            // Ensure images are the same size
            if (baselineImage.Width != actualImage.Width || baselineImage.Height != actualImage.Height)
            {
                throw new InvalidOperationException("Screenshots must have the same dimensions for comparison");
            }

            // Convert to bitmap for pixel access
            using var baselineBitmap = new Bitmap(baselineImage);
            using var actualBitmap = new Bitmap(actualImage);

            long totalPixels = baselineBitmap.Width * baselineBitmap.Height;
            long differentPixels = 0;

            // Compare each pixel
            for (int y = 0; y < baselineBitmap.Height; y++)
            {
                for (int x = 0; x < baselineBitmap.Width; x++)
                {
                    var baselinePixel = baselineBitmap.GetPixel(x, y);
                    var actualPixel = actualBitmap.GetPixel(x, y);

                    // Calculate color difference
                    int redDiff = Math.Abs(baselinePixel.R - actualPixel.R);
                    int greenDiff = Math.Abs(baselinePixel.G - actualPixel.G);
                    int blueDiff = Math.Abs(baselinePixel.B - actualPixel.B);

                    // If any color channel differs significantly, count as different pixel
                    if (redDiff > 10 || greenDiff > 10 || blueDiff > 10)
                    {
                        differentPixels++;
                    }
                }
            }

            double differencePercentage = (double)differentPixels / totalPixels * 100.0;

            // Generate difference image if requested
            if (!string.IsNullOrWhiteSpace(diffOutputPath))
            {
                var directory = Path.GetDirectoryName(diffOutputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var diffBitmap = new Bitmap(baselineBitmap.Width, baselineBitmap.Height);

                for (int y = 0; y < baselineBitmap.Height; y++)
                {
                    for (int x = 0; x < baselineBitmap.Width; x++)
                    {
                        var baselinePixel = baselineBitmap.GetPixel(x, y);
                        var actualPixel = actualBitmap.GetPixel(x, y);

                        int redDiff = Math.Abs(baselinePixel.R - actualPixel.R);
                        int greenDiff = Math.Abs(baselinePixel.G - actualPixel.G);
                        int blueDiff = Math.Abs(baselinePixel.B - actualPixel.B);

                        // Highlight differences in red
                        if (redDiff > 10 || greenDiff > 10 || blueDiff > 10)
                        {
                            diffBitmap.SetPixel(x, y, Color.FromArgb(255, Math.Min(255, redDiff * 5), 0, 0));
                        }
                        else
                        {
                            diffBitmap.SetPixel(x, y, Color.FromArgb(255, 0, 0, 0)); // Black for matching pixels
                        }
                    }
                }

                diffBitmap.Save(diffOutputPath, ImageFormat.Png);
            }

            return differencePercentage;
        }
        catch (Exception ex) when (!(ex is ArgumentException || ex is FileNotFoundException || ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Failed to compare screenshots: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates that a screenshot matches a baseline within a specified threshold.
    /// </summary>
    /// <param name="baselinePath">Path to the baseline screenshot.</param>
    /// <param name="actualPath">Path to the actual screenshot.</param>
    /// <param name="thresholdPercentage">Maximum allowed difference percentage (default: 1.0%).</param>
    /// <param name="diffOutputPath">Optional path to save the difference image if validation fails.</param>
    /// <returns>True if the screenshots match within the threshold; otherwise, false.</returns>
    public static bool ValidateScreenshotMatch(string baselinePath, string actualPath, double thresholdPercentage = 1.0, string? diffOutputPath = null)
    {
        if (thresholdPercentage < 0)
            throw new ArgumentOutOfRangeException(nameof(thresholdPercentage), "Threshold percentage must be non-negative");

        try
        {
            double difference = CompareScreenshots(baselinePath, actualPath, diffOutputPath);
            return difference <= thresholdPercentage;
        }
        catch (FileNotFoundException)
        {
            // If baseline doesn't exist, we can't validate
            return false;
        }
        catch (InvalidOperationException)
        {
            // If images are incompatible sizes, we can't validate
            return false;
        }
    }

    /// <summary>
    /// Resizes an image to the specified dimensions.
    /// </summary>
    /// <param name="sourcePath">Path to the source image.</param>
    /// <param name="destinationPath">Path where the resized image will be saved.</param>
    /// <param name="width">Target width.</param>
    /// <param name="height">Target height.</param>
    public static void ResizeImage(string sourcePath, string destinationPath, int width, int height)
    {
        Guard.AgainstNullOrEmpty(sourcePath, nameof(sourcePath));
        Guard.AgainstNullOrEmpty(destinationPath, nameof(destinationPath));

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Source image not found", sourcePath);

        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive");

        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive");

        try
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var sourceImage = Image.FromFile(sourcePath);
            using var resizedImage = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(resizedImage);

            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(sourceImage, 0, 0, width, height);

            resizedImage.Save(destinationPath, ImageFormat.Png);
        }
        catch (Exception ex) when (!(ex is ArgumentException || ex is FileNotFoundException || ex is ArgumentOutOfRangeException))
        {
            throw new InvalidOperationException($"Failed to resize image: {ex.Message}", ex);
        }
    }
}