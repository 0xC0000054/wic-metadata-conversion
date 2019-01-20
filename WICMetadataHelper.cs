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
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WICMetadataDemo
{
	internal static class WICMetadataHelper
	{
		/// <summary>
		/// Writes all of the meta-data fields to standard output.
		/// </summary>
		/// <param name="metadata">The meta-data.</param>
		/// <exception cref="ArgumentNullException"><paramref name="metadata"/> is null.</exception>
		internal static void DumpToStdOut(BitmapMetadata metadata)
		{
			if (metadata == null)
			{
				throw new ArgumentNullException(nameof(metadata));
			}

			DumpMetadataToStdOutRecursive(metadata);
		}

		/// <summary>
		/// Gets the EXIF comment.
		/// </summary>
		/// <param name="metadata">The meta-data.</param>
		/// <returns>The EXIF comment.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="metadata"/> is null.</exception>
		internal static string GetEXIFComment(BitmapMetadata metadata)
		{
			if (metadata == null)
			{
				throw new ArgumentNullException(nameof(metadata));
			}

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

			string comment = string.Empty;

			BitmapMetadata exif = GetEXIFMetadata(metadata, format);

			if (exif != null)
			{
				comment = exif.GetQuery("/{ushort=37510}") as string;
			}

			return comment;
		}

		/// <summary>
		/// Gets the XMP description.
		/// </summary>
		/// <param name="metadata">The meta-data.</param>
		/// <returns>The XMP description.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="metadata"/> is null.</exception>
		internal static string GetXMPDescription(BitmapMetadata metadata)
		{
			if (metadata == null)
			{
				throw new ArgumentNullException(nameof(metadata));
			}

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

			string description = string.Empty;

			BitmapMetadata xmp = GetXMPMetadata(metadata, format);

			if (xmp != null)
			{
				description = xmp.GetQuery("/dc:description/x-default") as string;
			}

			return description;
		}


		/// <summary>
		/// Converts the meta-data to the encoder format.
		/// </summary>
		/// <param name="metadata">The meta-data.</param>
		/// <param name="encoder">The encoder.</param>
		/// <returns>The converted meta-data, or null.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="metadata"/> is null.
		/// or
		/// <paramref name="encoder"/> is null.
		/// </exception>
		internal static BitmapMetadata ConvertSaveMetaDataFormat(BitmapMetadata metadata, BitmapEncoder encoder)
		{
			if (metadata == null)
			{
				throw new ArgumentNullException(nameof(metadata));
			}
			if (encoder == null)
			{
				throw new ArgumentNullException(nameof(encoder));
			}

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

			Type encoderType = encoder.GetType();

			if (encoderType == typeof(TiffBitmapEncoder))
			{
				if (format == "tiff")
				{
					return metadata;
				}
				else
				{
					return ConvertMetadataToTIFF(metadata);
				}
			}
			else if (encoderType == typeof(JpegBitmapEncoder))
			{
				if (format == "jpg")
				{
					return metadata;
				}
				else
				{
					return ConvertMetadataToJPEG(metadata, format);
				}
			}
			else if (encoderType == typeof(PngBitmapEncoder))
			{
				if (format == "png")
				{
					return metadata;
				}
				else
				{
					return ConvertMetadataToPNG(metadata, format);
				}
			}
			else if (encoderType == typeof(WmpBitmapEncoder))
			{
				if (format == "wmphoto")
				{
					return metadata;
				}
				else
				{
					return ConvertMetadataToWMPhoto(metadata, format);
				}
			}

			return null;
		}

		private static void CopySubIFDRecursive(ref BitmapMetadata parent, BitmapMetadata ifd, string query)
		{
			if (!parent.ContainsQuery(query))
			{
				parent.SetQuery(query, new BitmapMetadata(ifd.Format));
			}

			foreach (string tag in ifd)
			{
				object value = ifd.GetQuery(tag);

				if (value is BitmapMetadata ifdSub)
				{
					CopySubIFDRecursive(ref parent, ifdSub, query + tag);
				}
				else
				{
					parent.SetQuery(query + tag, value);
				}
			}
		}

		private static void DumpMetadataToStdOutRecursive(BitmapMetadata metadata, string query = "")
		{
			foreach (string tag in metadata)
			{
				object value = metadata.GetQuery(tag);

				if (value is BitmapMetadata subItem)
				{
					DumpMetadataToStdOutRecursive(subItem, query + tag);
				}
				else
				{
					Console.WriteLine("{0}: {1}", query + tag, value);
				}
			}
		}

		private static BitmapMetadata GetEXIFMetadata(BitmapMetadata metadata, string format)
		{
			BitmapMetadata exif = null;
			// GIF and PNG files do not contain EXIF meta data.
			if (format != "gif" && format != "png")
			{
				try
				{
					if (format == "jpg")
					{
						exif = metadata.GetQuery("/app1/ifd/exif") as BitmapMetadata;
					}
					else
					{
						exif = metadata.GetQuery("/ifd/exif") as BitmapMetadata;
					}
				}
				catch (IOException)
				{
					// WINCODEC_ERR_INVALIDQUERYREQUEST
				}
			}

			return exif;
		}

		/// <summary>
		/// Loads the PNG XMP meta data using a dummy TIFF.
		/// </summary>
		/// <param name="xmp">The XMP string to load.</param>
		/// <returns>The loaded XMP block, or null.</returns>
		private static BitmapMetadata LoadPNGMetadata(string xmp)
		{
			BitmapMetadata xmpData = null;

			using (MemoryStream stream = new MemoryStream())
			{
				// PNG stores the XMP meta-data in an iTXt chunk as an UTF8 encoded string,
				// so we have to save it to a dummy tiff and grab the XMP meta-data on load.
				BitmapMetadata tiffMetadata = new BitmapMetadata("tiff");
				tiffMetadata.SetQuery("/ifd/xmp", new BitmapMetadata("xmp"));
				tiffMetadata.SetQuery("/ifd/xmp", Encoding.UTF8.GetBytes(xmp));

				BitmapSource source = BitmapSource.Create(1, 1, 96.0, 96.0, PixelFormats.Gray8, null, new byte[] { 255 }, 1);
				TiffBitmapEncoder encoder = new TiffBitmapEncoder();
				encoder.Frames.Add(BitmapFrame.Create(source, null, tiffMetadata, null));
				encoder.Save(stream);

				TiffBitmapDecoder dec = new TiffBitmapDecoder(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);

				if (dec.Frames.Count == 1)
				{
					if (dec.Frames[0].Metadata is BitmapMetadata meta)
					{
						xmpData = meta.GetQuery("/ifd/xmp") as BitmapMetadata;
					}
				}
			}

			return xmpData;
		}

		private static BitmapMetadata GetXMPMetadata(BitmapMetadata metadata, string format)
		{
			BitmapMetadata xmp = null;

			// GIF files do not contain frame level XMP meta data.
			if (format != "gif")
			{
				try
				{
					if (format == "png")
					{
						if (metadata.GetQuery("/iTXt") is BitmapMetadata textChunk)
						{
							string keyword = textChunk.GetQuery("/Keyword") as string;

							if (string.Equals(keyword, "XML:com.adobe.xmp", StringComparison.Ordinal))
							{
								string data = textChunk.GetQuery("/TextEntry") as string;

								if (!string.IsNullOrEmpty(data))
								{
									xmp = LoadPNGMetadata(data);
								}
							}
						}
					}
					else if (format == "jpg")
					{
						xmp = metadata.GetQuery("/xmp") as BitmapMetadata;
					}
					else
					{
						try
						{
							xmp = metadata.GetQuery("/ifd/xmp") as BitmapMetadata;
						}
						catch (IOException)
						{
							// WINCODEC_ERR_INVALIDQUERYREQUEST
						}

						if (xmp == null)
						{
							// Some codecs may store the XMP data outside of the IFD block.
							xmp = metadata.GetQuery("/xmp") as BitmapMetadata;
						}
					}
				}
				catch (IOException)
				{
					// WINCODEC_ERR_INVALIDQUERYREQUEST
				}
			}

			return xmp;
		}

		private static BitmapMetadata GetIPTCMetadata(BitmapMetadata metaData, string format)
		{
			BitmapMetadata iptc = null;
			// GIF and PNG files do not contain IPTC meta data.
			if (format != "gif" && format != "png")
			{
				try
				{
					if (format == "jpg")
					{
						iptc = metaData.GetQuery("/app13/irb/8bimiptc/iptc") as BitmapMetadata;
					}
					else
					{
						try
						{
							iptc = metaData.GetQuery("/ifd/iptc") as BitmapMetadata;
						}
						catch (IOException)
						{
							// WINCODEC_ERR_INVALIDQUERYREQUEST
						}

						if (iptc == null)
						{
							iptc = metaData.GetQuery("/ifd/irb/8bimiptc/iptc") as BitmapMetadata;
						}
					}
				}
				catch (IOException)
				{
					// WINCODEC_ERR_INVALIDQUERYREQUEST
				}
			}

			return iptc;
		}

		#region Save format conversion
		/// <summary>
		/// Converts the meta-data to TIFF format.
		/// </summary>
		/// <param name="metadata">The meta data.</param>
		/// <returns>The converted meta data or null</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="metadata"/> is null.
		/// </exception>
		private static BitmapMetadata ConvertMetadataToTIFF(BitmapMetadata metadata)
		{
			if (metadata == null)
			{
				throw new ArgumentNullException(nameof(metadata));
			}

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

			if (format != "tiff")
			{
				BitmapMetadata exif = GetEXIFMetadata(metadata, format);
				BitmapMetadata xmp = GetXMPMetadata(metadata, format);
				BitmapMetadata iptc = GetIPTCMetadata(metadata, format);

				if (exif == null && xmp == null && iptc == null)
				{
					return null;
				}

				BitmapMetadata tiffMetadata = new BitmapMetadata("tiff");

				if (exif != null)
				{
					tiffMetadata.SetQuery("/ifd/exif", new BitmapMetadata("exif"));

					foreach (string tag in exif)
					{
						object value = exif.GetQuery(tag);

						if (value is BitmapMetadata exifSub)
						{
							CopySubIFDRecursive(ref tiffMetadata, exifSub, "/ifd/exif" + tag);
						}
						else
						{
							tiffMetadata.SetQuery("/ifd/exif" + tag, value);
						}
					}
				}

				if (xmp != null)
				{
					tiffMetadata.SetQuery("/ifd/xmp", new BitmapMetadata("xmp"));

					foreach (string tag in xmp)
					{
						object value = xmp.GetQuery(tag);

						if (value is BitmapMetadata xmpSub)
						{
							CopySubIFDRecursive(ref tiffMetadata, xmpSub, "/ifd/xmp" + tag);
						}
						else
						{
							tiffMetadata.SetQuery("/ifd/xmp" + tag, value);
						}
					}
				}

				if (iptc != null)
				{
					tiffMetadata.SetQuery("/ifd/iptc", new BitmapMetadata("iptc"));

					foreach (string tag in iptc)
					{
						object value = iptc.GetQuery(tag);

						if (value is BitmapMetadata iptcSub)
						{
							CopySubIFDRecursive(ref tiffMetadata, iptcSub, "/ifd/iptc" + tag);
						}
						else
						{
							tiffMetadata.SetQuery("/ifd/iptc" + tag, value);
						}
					}
				}

				return tiffMetadata;
			}

			return metadata;
		}

		private static BitmapMetadata ConvertMetadataToJPEG(BitmapMetadata metadata, string format)
		{
			BitmapMetadata exif = GetEXIFMetadata(metadata, format);
			BitmapMetadata xmp = GetXMPMetadata(metadata, format);
			BitmapMetadata iptc = GetIPTCMetadata(metadata, format);

			if (exif == null && xmp == null && iptc == null)
			{
				return null;
			}

			BitmapMetadata jpegMetadata = new BitmapMetadata("jpg");

			if (exif != null)
			{
				jpegMetadata.SetQuery("/app1/ifd/exif", new BitmapMetadata("exif"));

				foreach (string tag in exif)
				{
					object value = exif.GetQuery(tag);

					if (value is BitmapMetadata exifSub)
					{
						CopySubIFDRecursive(ref jpegMetadata, exifSub, "/app1/ifd/exif" + tag);
					}
					else
					{
						jpegMetadata.SetQuery("/app1/ifd/exif" + tag, value);
					}
				}
			}

			if (xmp != null)
			{
				jpegMetadata.SetQuery("/xmp", new BitmapMetadata("xmp"));

				foreach (string tag in xmp)
				{
					object value = xmp.GetQuery(tag);

					if (value is BitmapMetadata xmpSub)
					{
						CopySubIFDRecursive(ref jpegMetadata, xmpSub, "/xmp" + tag);
					}
					else
					{
						jpegMetadata.SetQuery("/xmp" + tag, value);
					}
				}
			}

			if (iptc != null)
			{
				jpegMetadata.SetQuery("/app13/irb/8bimiptc/iptc", new BitmapMetadata("iptc"));

				foreach (string tag in iptc)
				{
					object value = iptc.GetQuery(tag);

					if (value is BitmapMetadata iptcSub)
					{
						CopySubIFDRecursive(ref jpegMetadata, iptcSub, "/app13/irb/8bimiptc/iptc" + tag);
					}
					else
					{
						jpegMetadata.SetQuery("/app13/irb/8bimiptc/iptc" + tag, value);
					}
				}
			}

			return jpegMetadata;
		}

		private static byte[] ExtractXMPPacket(BitmapMetadata xmp)
		{
			BitmapMetadata tiffMetaData = new BitmapMetadata("tiff");
			tiffMetaData.SetQuery("/ifd/xmp", new BitmapMetadata("xmp"));

			foreach (string tag in xmp)
			{
				object value = xmp.GetQuery(tag);

				if (value is BitmapMetadata xmpSub)
				{
					CopySubIFDRecursive(ref tiffMetaData, xmpSub, "/ifd/xmp" + tag);
				}
				else
				{
					tiffMetaData.SetQuery("/ifd/xmp" + tag, value);
				}
			}

			byte[] xmpBytes = null;

			using (MemoryStream stream = new MemoryStream())
			{
				// Create a dummy tiff to extract the XMP packet from.
				BitmapSource source = BitmapSource.Create(1, 1, 96.0, 96.0, PixelFormats.Gray8, null, new byte[] { 255 }, 1);
				TiffBitmapEncoder encoder = new TiffBitmapEncoder();
				encoder.Frames.Add(BitmapFrame.Create(source, null, tiffMetaData, null));
				encoder.Save(stream);

				xmpBytes = TiffReader.ExtractXMP(stream);
			}

			return xmpBytes;
		}

		private static BitmapMetadata ConvertMetadataToPNG(BitmapMetadata metadata, string format)
		{
			BitmapMetadata xmp = GetXMPMetadata(metadata, format);

			if (xmp != null)
			{
				byte[] packet = ExtractXMPPacket(xmp);

				if (packet != null)
				{
					BitmapMetadata pngMetadata = new BitmapMetadata("png");
					pngMetadata.SetQuery("/iTXt", new BitmapMetadata("iTXt"));

					// The Keyword property is an ANSI string (VT_LPSTR), which must passed as a char array in order for it to be marshaled correctly.
					char[] keywordChars = "XML:com.adobe.xmp".ToCharArray();
					pngMetadata.SetQuery("/iTXt/Keyword", keywordChars);

					pngMetadata.SetQuery("/iTXt/TextEntry", Encoding.UTF8.GetString(packet));

					return pngMetadata;
				}
			}

			return null;
		}

		private static BitmapMetadata ConvertMetadataToWMPhoto(BitmapMetadata metadata, string format)
		{
			BitmapMetadata exif = GetEXIFMetadata(metadata, format);
			BitmapMetadata xmp = GetXMPMetadata(metadata, format);
			BitmapMetadata iptc = GetIPTCMetadata(metadata, format);

			if (exif == null && xmp == null && iptc == null)
			{
				return null;
			}

			BitmapMetadata wmpMetadata = new BitmapMetadata("wmphoto");

			if (exif != null)
			{
				wmpMetadata.SetQuery("/ifd/exif", new BitmapMetadata("exif"));

				foreach (string tag in exif)
				{
					object value = exif.GetQuery(tag);

					if (value is BitmapMetadata exifSub)
					{
						CopySubIFDRecursive(ref wmpMetadata, exifSub, "/ifd/exif" + tag);
					}
					else
					{
						wmpMetadata.SetQuery("/ifd/exif" + tag, value);
					}
				}
			}

			if (xmp != null)
			{
				wmpMetadata.SetQuery("/ifd/xmp", new BitmapMetadata("xmp"));

				foreach (string tag in xmp)
				{
					object value = xmp.GetQuery(tag);

					if (value is BitmapMetadata xmpSub)
					{
						CopySubIFDRecursive(ref wmpMetadata, xmpSub, "/ifd/xmp" + tag);
					}
					else
					{
						wmpMetadata.SetQuery("/ifd/xmp" + tag, value);
					}
				}
			}

			if (iptc != null)
			{
				wmpMetadata.SetQuery("/ifd/iptc", new BitmapMetadata("iptc"));

				foreach (string tag in iptc)
				{
					object value = iptc.GetQuery(tag);

					if (value is BitmapMetadata iptcSub)
					{
						CopySubIFDRecursive(ref wmpMetadata, iptcSub, "/ifd/iptc" + tag);
					}
					else
					{
						wmpMetadata.SetQuery("/ifd/iptc" + tag, value);
					}
				}
			}

			return wmpMetadata;
		}
		#endregion

		private static class TiffReader
		{
			private enum DataType : ushort
			{
				Byte = 1,
				Ascii = 2,
				Short = 3,
				Long = 4,
				Rational = 5,
				SByte = 6,
				Undefined = 7,
				SShort = 8,
				SLong = 9,
				SRational = 10,
				Float = 11,
				Double = 12
			}

			private struct IFD
			{
				public readonly ushort tag;
				public readonly DataType type;
				public readonly uint count;
				public readonly uint offset;

				public IFD(Stream stream, bool littleEndian)
				{
					tag = ReadShort(stream, littleEndian);
					type = (DataType)ReadShort(stream, littleEndian);
					count = ReadLong(stream, littleEndian);
					offset = ReadLong(stream, littleEndian);
				}
			}

			private static ushort ReadShort(Stream stream, bool littleEndian)
			{
				int byte1 = stream.ReadByte();
				if (byte1 == -1)
				{
					throw new EndOfStreamException();
				}

				int byte2 = stream.ReadByte();
				if (byte2 == -1)
				{
					throw new EndOfStreamException();
				}

				if (littleEndian)
				{
					return (ushort)(byte1 | (byte2 << 8));
				}
				else
				{
					return (ushort)((byte1 << 8) | byte2);
				}
			}

			private static uint ReadLong(Stream stream, bool littleEndian)
			{
				int byte1 = stream.ReadByte();
				if (byte1 == -1)
				{
					throw new EndOfStreamException();
				}

				int byte2 = stream.ReadByte();
				if (byte2 == -1)
				{
					throw new EndOfStreamException();
				}

				int byte3 = stream.ReadByte();
				if (byte3 == -1)
				{
					throw new EndOfStreamException();
				}

				int byte4 = stream.ReadByte();
				if (byte4 == -1)
				{
					throw new EndOfStreamException();
				}

				if (littleEndian)
				{
					return (uint)(byte1 | (byte2 << 8) | (byte3 << 16) | (byte4 << 24));
				}
				else
				{
					return (uint)((byte1 << 24) | (byte2 << 16) | (byte3 << 8) | byte4);
				}
			}

			private const ushort LittleEndianByteOrder = 0x4949;
			private const ushort TIFFSignature = 42;
			private const ushort XmpTag = 700;

			/// <summary>
			/// Extracts the XMP packet from a TIFF file.
			/// </summary>
			/// <param name="stream">The stream to read.</param>
			/// <returns>The extracted XMP packet, or null.</returns>
			internal static byte[] ExtractXMP(Stream stream)
			{
				stream.Position = 0L;

				try
				{
					ushort byteOrder = ReadShort(stream, false);

					bool littleEndian = byteOrder == LittleEndianByteOrder;

					ushort signature = ReadShort(stream, littleEndian);

					if (signature == TIFFSignature)
					{
						uint ifdOffset = ReadLong(stream, littleEndian);
						stream.Seek(ifdOffset, SeekOrigin.Begin);

						int ifdCount = ReadShort(stream, littleEndian);

						for (int i = 0; i < ifdCount; i++)
						{
							IFD ifd = new IFD(stream, littleEndian);

							if (ifd.tag == XmpTag && (ifd.type == DataType.Byte || ifd.type == DataType.Undefined))
							{
								stream.Seek(ifd.offset, SeekOrigin.Begin);

								int count = (int)ifd.count;

								byte[] xmpBytes = new byte[count];

								int numBytesToRead = count;
								int numBytesRead = 0;
								do
								{
									int n = stream.Read(xmpBytes, numBytesRead, numBytesToRead);
									numBytesRead += n;
									numBytesToRead -= n;
								} while (numBytesToRead > 0);

								return xmpBytes;
							}
						}
					}
				}
				catch (EndOfStreamException)
				{
				}

				return null;
			}
		}
	}
}
