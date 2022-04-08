using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using CliWrap;
using CliWrap.Buffered;
using WindowsInput;
using WindowsInput.Native;

namespace HotReload 
{
    public class Program
    {
		// Basic Win32 API interface taken from https://web.archive.org/web/20100514102626/http://forums.fanatic.net.nz/index.php?showtopic=18873

 		public const int SW_SHOWDEFAULT = 10;
 		public const int SW_RESTORE = 9;
 		public const int SW_SHOW = 5;
	    public const uint WM_DROPFILES = 0x0233;



		[DllImport("Kernel32.dll", SetLastError = true)]
		public static extern int GlobalLock(IntPtr Handle);
		
		[DllImport("Kernel32.dll", SetLastError = true)]
		public static extern int GlobalUnlock(IntPtr Handle);
		
		[return: MarshalAs(UnmanagedType.Bool)]
		[DllImport("user32.dll", SetLastError = true)]
		static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern int SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		static extern public bool BringWindowToTop(IntPtr hWnd);

		[DllImport("user32.dll")]
		static extern public uint SendInput(uint cInputs, IntPtr pInputs, int cbSize);

		private delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

		[DllImport("USER32.DLL")]
		private static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

		[DllImport("USER32.DLL")]
		private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

		[DllImport("USER32.DLL")]
		private static extern int GetWindowTextLength(IntPtr hWnd);

		[DllImport("USER32.DLL")]
		private static extern bool IsWindowVisible(IntPtr hWnd);

		[DllImport("USER32.DLL")]
		private static extern IntPtr GetShellWindow();



		[Serializable]
		[StructLayout(LayoutKind.Sequential)]
		public struct Point
		{
			public Int32 X;
			public Int32 Y;

			public Point(Int32 x, Int32 y)
			{
				this.X = x;
				this.Y = y;
			}
		}

		[Serializable]
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		public struct Dropfiles
		{
			public int size;
			public Point pt;
			public bool fND;
			public bool wide;
		}



		public static IDictionary<string, IntPtr> GetOpenWindows()
		{
			IntPtr shellWindow = GetShellWindow();
			Dictionary<string, IntPtr> windows = new Dictionary<string, IntPtr>();

			EnumWindows(delegate(IntPtr hWnd, int lParam)
			{
			if (hWnd == shellWindow) return true;
			if (!IsWindowVisible(hWnd)) return true;

			int length = GetWindowTextLength(hWnd);
			if (length == 0) return true;

			StringBuilder builder = new StringBuilder(length);
			GetWindowText(hWnd, builder, length + 1);

			windows[builder.ToString()] = hWnd;
			return true;

			}, 0);

			return windows;
		}



		public static byte[] RawSerialize(object o)
		{
			var size = Marshal.SizeOf(o);
			var buffer = Marshal.AllocHGlobal(size);
			Marshal.StructureToPtr(o, buffer, false);
			var bytes = new byte[size];
			Marshal.Copy(buffer, bytes, 0, size);
			Marshal.FreeHGlobal(buffer);
			return bytes;
		}



        public static IntPtr GetWindowHandleByTitle(string title)
        {
			var windows = GetOpenWindows();
			return windows.First(x => x.Key.Contains(title)).Value;
        }



		public static int WriteBytes(byte[] source, IntPtr dest, int offset)
		{
			var i = 0;
			while (true)
            {
				if (i > source.Length - 1)
				{
					break;
				}

                Marshal.WriteByte(dest, i + offset, source[i]);
				i++;
            }
			return i; 
		}



		public static async void DropFileOnWindow(string filePath, string window)
		{
            var hWnd = GetWindowHandleByTitle(window);
			var old = GetForegroundWindow();
			SetForegroundWindow(hWnd);




            var dropfiles = new Dropfiles();
			dropfiles.size = 20;
			dropfiles.pt = new Point(20, 20);
			dropfiles.fND = false;
			dropfiles.wide = false;
			var dropfiles_asBytes = RawSerialize(dropfiles);

            var target = Path.GetFullPath(filePath);
            if (Directory.Exists(target) == false)
            {
				throw new Exception($@"No directory found at location ""{target}"".");
            }
            target += '\0';
            var target_asBytes = ASCIIEncoding.ASCII.GetBytes(target);
			var totalLength = dropfiles_asBytes.Length + target_asBytes.Length;

			var p = Marshal.AllocHGlobal(totalLength);
			GlobalLock(p);
			var p_i = 0;
			p_i = WriteBytes(dropfiles_asBytes, p, 0);
			p_i = WriteBytes(target_asBytes, p, p_i);
            GlobalUnlock(p);


			// The WM_DROPFILES message is not application-generatable. It requires special handling (HGLOBAL marshaling) which is not available to applications. The operating system does wacky internal stuff, and if you try to post the message yourself, the wacky internal stuff will not work. (For example, it will call GlobalSize to get the size of the memory block, but since you didn't allocate the memory with GlobalAlloc, you get garbage.) – Raymond Chen Aug 27, 2014 at 6:48
			// Basically, this is a hack.
			// A more correct implementation might make use of the docs here: https://docs.microsoft.com/en-us/previous-versions/windows/desktop/legacy/bb776905(v=vs.85)?redirectedfrom=MSDN
            PostMessage(hWnd, WM_DROPFILES, p, IntPtr.Zero);
			// Internally, the handler for WM_DROPFILES frees the memory
			// so no need to call it here.
			//Marshal.FreeHGlobal(p);

			//BringWindowToTop(hwnd);
			//SendKeys.SendWait("^(r)");
			var sim = new InputSimulator();
			sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_R);


			SetForegroundWindow(old);
		}



        public static int Main(string[] args)
        {
			try
			{
				DropFileOnWindow("out.pdx", "Playdate");
				return 0;
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex.Message);
				return 1;
			}
        }
    }
}