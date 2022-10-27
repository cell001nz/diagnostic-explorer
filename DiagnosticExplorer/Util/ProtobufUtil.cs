using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace DiagnosticExplorer.Util
{
	public static class ProtobufUtil
	{

		public static byte[] Compress<T>(T obj, int compressThreshold)
		{
			MemoryStream stream = new();
			stream.WriteByte(0);
			ProtoBuf.Serializer.Serialize(stream, obj);

			if (stream.Length <= compressThreshold)
			{
//				Debug.WriteLine($"ProtobufUtil.Compress<{typeof(T).Name}> raw length is {stream.Length}");
				return stream.ToArray();
			}

			MemoryStream compressedStream = new();
			compressedStream.WriteByte(1);
			using (GZipStream gstream = new(compressedStream, CompressionMode.Compress, leaveOpen: true))
			{
				stream.Position = 1;
				stream.CopyTo(gstream);
			}
			byte[] compressed = compressedStream.ToArray();
//			Debug.WriteLine($"ProtobufUtil.Compress<{typeof(T).Name}> uncompressed: {stream.Length} compressed: {compressed.Length}");

			return compressed;
		}


		public static T Decompress<T>(byte[] body)
		{
			MemoryStream bodyStream = new(body, 1, body.Length - 1);

			if (body[0] == 0)
				return Serializer.Deserialize<T>(bodyStream);

			GZipStream gstream = new(bodyStream, CompressionMode.Decompress);
			return Serializer.Deserialize<T>(gstream);
		}
	}
}