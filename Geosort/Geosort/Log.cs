using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geosort
{
	public class Log
	{
		const string FILE_PATH = "log.txt";

		public static void WriteLine(object message, bool time = true)
		{
			using (StreamWriter stream = new StreamWriter(FILE_PATH, true))
			{
				if (time) stream.WriteLine($"[{DateTime.Now}]: {message}");
				else stream.WriteLine(message);
			}
		}
		public static void Clear()
		{
			using (StreamWriter stream = new StreamWriter(FILE_PATH))
			{
				stream.Write("");
			}
		}
	}
}
