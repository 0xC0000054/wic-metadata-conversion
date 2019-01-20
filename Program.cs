////////////////////////////////////////////////////////////////////////
//
// This file is part of wic-metadata-conversion, an application that
// demonstrates WIC and GDI+ meta-data support.
//
// Copyright (c) 2019 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace WICMetadataDemo
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            CommandLineParser parser = new CommandLineParser(args);

            if (parser.ShowHelp)
            {
                Console.WriteLine("Usage: WICMetadataDemo.exe [-dump] [-help] [image].");
            }
            else
            {
                string imagePath = parser.ImageFileName;
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    imagePath = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "GreenSquare.jpg");
                }

                try
                {
                    BitmapFrame frame = BitmapFrame.Create(new Uri(imagePath));

                    BitmapMetadata metadata = null;
                    try
                    {
                        metadata = frame.Metadata as BitmapMetadata;
                    }
                    catch (NotSupportedException)
                    {
                    }

                    if (metadata != null)
                    {
                        string format = string.Empty;

                        try
                        {
                            format = metadata.Format; // Some WIC codecs do not implement the format property.
                        }
                        catch (ArgumentException)
                        {
                        }
                        catch (NotSupportedException)
                        {
                        }

                        if (parser.DumpMetadata)
                        {
                            Console.WriteLine("{0} format WIC metadata\n", string.IsNullOrEmpty(format) ? "Unspecified" : format.ToUpperInvariant());

                            WICMetadataHelper.DumpToStdOut(metadata);
                            Console.WriteLine();
                        }
                        else
                        {
                            TestWICMetadataPersistance(metadata);
                            TestGDIPlusMetadataPersistance(imagePath);
                        }
                    }
                    else
                    {
                        Console.WriteLine("The image does not contain any WIC metadata.\n");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            Console.WriteLine("Press any key to exit.");
            Console.ReadLine();
        }

        private static void TestWICMetadataPersistance(BitmapMetadata metadata)
        {
            Console.WriteLine("Reading original metadata using WIC:\n");
            ReadMetadataUsingWIC(metadata);

            BitmapMetadata persisted = GetPersistedWICMetadata(metadata);

            if (persisted != null)
            {
                Console.WriteLine("Reading persisted metadata using WIC:\n");
                ReadMetadataUsingWIC(metadata);
            }
            else
            {
                Console.WriteLine("The persisted image does not contain any WIC metadata.\n");
            }
        }

        private static BitmapMetadata GetPersistedWICMetadata(BitmapMetadata original)
        {
            BitmapMetadata newMetadata = null;

            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            try
            {
                BitmapSource source = BitmapSource.Create(1, 1, 96.0, 96.0, System.Windows.Media.PixelFormats.Gray8, null, new byte[] { 255 }, 1);

                TiffBitmapEncoder encoder = new TiffBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source, null, WICMetadataHelper.ConvertSaveMetaDataFormat(original, encoder), null));

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    encoder.Save(stream);
                }

                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    BitmapFrame frame = BitmapFrame.Create(stream);
                    try
                    {
                        BitmapMetadata temp = frame.Metadata as BitmapMetadata;

                        newMetadata = temp?.Clone();
                    }
                    catch (NotSupportedException)
                    {
                    }
                }
            }
            finally
            {
                File.Delete(path);
            }

            return newMetadata;
        }

        private static void ReadMetadataUsingWIC(BitmapMetadata metadata)
        {
            string comment = WICMetadataHelper.GetEXIFComment(metadata);
            if (string.IsNullOrEmpty(comment))
            {
                Console.WriteLine("EXIF comment not found");
            }
            else
            {
                Console.WriteLine("EXIF comment: " + comment);
            }

            string xmpDesc = WICMetadataHelper.GetXMPDescription(metadata);
            if (string.IsNullOrEmpty(xmpDesc))
            {
                Console.WriteLine("XMP description not found");
            }
            else
            {
                Console.WriteLine("XMP description: " + xmpDesc);
            }

            Console.WriteLine();
        }

        private static void TestGDIPlusMetadataPersistance(string filename)
        {
            using (Image image = Image.FromFile(filename))
            {
                PropertyItem[] originalMetadata = image.PropertyItems ?? Array.Empty<PropertyItem>();

                if (originalMetadata.Length > 0)
                {
                    Console.WriteLine("Reading original metadata using GDIPlus:\n");

                    ReadMetadataUsingGDIPlus(originalMetadata);

                    PropertyItem[] persisted = GetPersistedGDIPlusMetadata(image) ?? Array.Empty<PropertyItem>();

                    if (persisted.Length > 0)
                    {
                        Console.WriteLine("Reading persisted metadata using GDI+:\n");
                        ReadMetadataUsingGDIPlus(persisted);
                    }
                    else
                    {
                        Console.WriteLine("The persisted image does not contain any GDI+ metadata.\n");
                    }
                }
                else
                {
                    Console.WriteLine("The original image does not contain any GDI+ metadata.\n");
                }
            }
        }

        private static PropertyItem[] GetPersistedGDIPlusMetadata(Image original)
        {
            PropertyItem[] newMetadata = null;

            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            try
            {
                original.Save(path, ImageFormat.Tiff);

                using (Image persisted = Image.FromFile(path))
                {
                    newMetadata = (PropertyItem[])persisted.PropertyItems?.Clone();
                }
            }
            finally
            {
                File.Delete(path);
            }

            return newMetadata;
        }

        private static void ReadMetadataUsingGDIPlus(PropertyItem[] propertyItems)
        {
            string comment = string.Empty;

            foreach (PropertyItem item in propertyItems)
            {
                if (item.Id == 37510)
                {
                    comment = Encoding.Unicode.GetString(item.Value).TrimEnd('\0');
                    break;
                }
            }

            if (string.IsNullOrEmpty(comment))
            {
                Console.WriteLine("EXIF comment not found");
            }
            else
            {
                Console.WriteLine("EXIF comment: " + comment);
            }

            Console.WriteLine("GDI+ does not support XMP.");

            Console.WriteLine();
        }
    }
}
