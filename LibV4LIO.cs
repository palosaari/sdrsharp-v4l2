using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;
using SDRSharp.Radio;

namespace SDRSharp.V4L2
{
	public class Mmap
	{
		public IntPtr start;
		public UInt32 length;
	}
	
	public unsafe class LibV4LIO : IFrontendController, IDisposable
	{
		/* Control classes */
		private const UInt32 V4L2_CTRL_CLASS_USER    = 0x00980000; /* Old-style 'user' controls */
		/* User-class control IDs */
		private const UInt32 V4L2_CID_BASE           = (V4L2_CTRL_CLASS_USER | 0x900);
		private const UInt32 V4L2_CID_USER_BASE      = V4L2_CID_BASE;

		private const UInt32 CID_SAMPLING_MODE       = ((V4L2_CID_USER_BASE | 0xf000) + 0);
		private const UInt32 CID_SAMPLING_RATE       = ((V4L2_CID_USER_BASE | 0xf000) + 1);
		private const UInt32 CID_SAMPLING_RESOLUTION = ((V4L2_CID_USER_BASE | 0xf000) + 2);
		private const UInt32 CID_TUNER_RF            = ((V4L2_CID_USER_BASE | 0xf000) + 10);
		private const UInt32 CID_TUNER_BW            = ((V4L2_CID_USER_BASE | 0xf000) + 11);
		private const UInt32 CID_TUNER_IF            = ((V4L2_CID_USER_BASE | 0xf000) + 12);
		private const UInt32 CID_TUNER_GAIN          = ((V4L2_CID_USER_BASE | 0xf000) + 13);
		
		private const UInt64 CMD64_VIDIOC_DQBUF        = 0xc0585611;
		private const UInt64 CMD64_VIDIOC_S_EXT_CTRLS  = 0xc0205648;
		private const UInt64 CMD64_VIDIOC_S_FMT        = 0xc0d05605;
		private const UInt64 CMD64_VIDIOC_S_FREQUENCY  = 0x402c5639;
		private const UInt64 CMD64_VIDIOC_QBUF	       = 0xc058560f;
		private const UInt64 CMD64_VIDIOC_QUERYBUF     = 0xc0585609;
		private const UInt64 CMD64_VIDIOC_QUERYCAP     = 0x80685600;
		private const UInt64 CMD64_VIDIOC_QUERYCTRL    = 0xc0445624;
		private const UInt64 CMD64_VIDIOC_QUERYSTD     = 0x8008563f;
		private const UInt64 CMD64_VIDIOC_REQBUFS      = 0xc0145608;
		private const UInt64 CMD64_VIDIOC_STREAMOFF    = 0x40045613;
		private const UInt64 CMD64_VIDIOC_STREAMON     = 0x40045612;
		private const UInt64 CMD64_VIDIOC_TRY_FMT      = 0xc0d05640;
		
		private const byte V4L2_BUF_TYPE_SDR_CAPTURE   = 11;
		private const byte V4L2_MEMORY_MMAP            = 1;

		private const byte V4L2_TUNER_ADC              = 4;
		private const byte V4L2_TUNER_RF               = 5;

		private const byte PROT_READ  = 0x1;		// page can be read
		private const byte PROT_WRITE = 0x2;		// page can be written
		private const byte MAP_SHARED = 0x01;		// Share changes
		
		private const int O_RDWR      = 0x0002; //open for reading and writing
		private const int O_NONBLOCK  = 0x0004; //no delay

		// pixformat V4L2 fourcc
		private const uint V4L2_PIX_FMT_SDR_U8         = 0x38305544;
		private const uint V4L2_PIX_FMT_SDR_U16LE      = 0x36315544;

		private long _frequency = 100000000; // 100 MHz
		private double _sampleRate = 2048000; // 2.048 Msps
		private SamplesAvailableDelegate _callback;
		private Thread _sampleThread;
		private int _fd;
		private string _dev_file = "/dev/swradio0";
		
		private bool streaming;
		// buffers
		private IntPtr [] mmap_start;
		private UInt32 [] mmap_length;
//		private Mmap [] mmap;
//		private List<Mmap> mmapList = new List<Mmap>();
		private uint n_buffers;
		private NativeMethods.v4l2_buffer buf;
		// pre-calculated LUT to speed up stream float conversion
		private static readonly float *_lutPtr;
		private static readonly UnsafeBuffer _lutBuffer = UnsafeBuffer.Create(65536, sizeof(float));
		private readonly ConfigDialog _gui;
		
		public bool IsStreaming
		{
			get { return _sampleThread != null; }
		}

		public bool IsSoundCardBased
		{
			get { return false; }
		}

		public string SoundCardHint
		{
			get { return string.Empty; }
		}
		
		public void ShowSettingGUI(IWin32Window parent)
		{
			Console.WriteLine("ShowSettingGUI()");
			_gui.Show();
		}

		public void HideSettingGUI()
		{
			Console.WriteLine("HideSettingGUI()");
			_gui.Hide();
		}
		
		private void vidioc_s_ext_ctrls_(UInt32 id, Int64 value, bool int64)
		{
			Console.WriteLine("vidioc_s_ext_ctrls_");
			
			NativeMethods.v4l2_ext_controls ext_ctrls = new NativeMethods.v4l2_ext_controls();
			NativeMethods.v4l2_ext_control ext_ctrl = new NativeMethods.v4l2_ext_control();

			ext_ctrl.id = (uint) id;
			ext_ctrl.size = 0;
			ext_ctrl.reserved2[0] = 0;
			if (int64)
			{
				ext_ctrl.value64 = value;
			}
			else
			{
				ext_ctrl.value = (int) value;
			}
			void* p_ext_ctrl = & ext_ctrl;

			ext_ctrls.ctrl_class = (uint) V4L2_CTRL_CLASS_USER;
			ext_ctrls.count = 1;
			ext_ctrls.error_idx = 0;
			ext_ctrls.reserved[0] = 0;
			ext_ctrls.reserved[1] = 0;
			ext_ctrls.controls = (IntPtr) p_ext_ctrl;

			//int v4l2_r = NativeMethods.v4l2_ioctl(_fd, CMD64_VIDIOC_S_EXT_CTRLS, ref ext_ctrls); 
			int v4l2_r = NativeMethods.ioctl(_fd, CMD64_VIDIOC_S_EXT_CTRLS, ref ext_ctrls); 
			Console.WriteLine("v4l2_ioctl CMD64_VIDIOC_S_EXT_CTRLS ret = {0} id = {1} value64 = {2}", v4l2_r, id, value);
		}
		
		private void vidioc_s_ext_ctrls(UInt32 id, Int64 value)
		{
			vidioc_s_ext_ctrls_(id, value, true);
		}

		private void vidioc_s_ext_ctrls(UInt32 id, Int32 value)
		{
			vidioc_s_ext_ctrls_(id, value, false);
		}
				
		public double Samplerate
		{
			get { return _sampleRate; }
			set
			{
				Console.WriteLine("Samplerate set {0}", value);
				int v4l2_r;
				_sampleRate = value;
				NativeMethods.v4l2_frequency frequency = new NativeMethods.v4l2_frequency();
				frequency.tuner = 0;
				frequency.type = V4L2_TUNER_ADC;
				frequency.frequency = (uint) value;
				//v4l2_r = NativeMethods.v4l2_ioctl(_fd, CMD64_VIDIOC_S_FREQUENCY, ref frequency);
				v4l2_r = NativeMethods.ioctl(_fd, CMD64_VIDIOC_S_FREQUENCY, ref frequency);
				Console.WriteLine("v4l2_ioctl CMD64_VIDIOC_S_FREQUENCY r = {0}", v4l2_r);
			}
		}

		public long Frequency
		{
			get { return _frequency; }
			set
			{
				Console.WriteLine("Frequency set {0}", value);
				int v4l2_r;
				_frequency = value;
				NativeMethods.v4l2_frequency frequency = new NativeMethods.v4l2_frequency();
				frequency.tuner = 1;
				frequency.type = V4L2_TUNER_RF;
				frequency.frequency = (uint) value;
				//v4l2_r = NativeMethods.v4l2_ioctl(_fd, CMD64_VIDIOC_S_FREQUENCY, ref frequency);
				v4l2_r = NativeMethods.ioctl(_fd, CMD64_VIDIOC_S_FREQUENCY, ref frequency);
				Console.WriteLine("v4l2_ioctl CMD64_VIDIOC_S_FREQUENCY r = {0}", v4l2_r);
			}
		}

		static LibV4LIO()
		{
			Console.WriteLine("LibV4LIO() static");
			// populate 16bit LUT
			_lutPtr = (float *)_lutBuffer;
			for (var i = 0; i < 65536; i++)
			{
				_lutPtr[i] = (i - 32767.5f) / 32767.5f;
			}
		}
		
		public LibV4LIO()
		{
			Console.WriteLine("LibV4LIO()");
			_gui = new ConfigDialog(this);
		}

		~LibV4LIO()
		{
			Console.WriteLine("~LibV4LIO()");
			GC.SuppressFinalize(this);
		}

		public void Dispose()
		{
			Console.WriteLine("Dispose");
			_gui.Dispose();
		}

		public void Open()
		{
			Console.WriteLine("Open");

			//_fd = NativeMethods.v4l2_open(_dev_file, O_RDWR);
			_fd = NativeMethods.open(_dev_file, O_RDWR);
			Console.WriteLine("fd = {0}", _fd);
			if (_fd < 0)
			{
				throw new ApplicationException("Cannot open V4L2 device. Is the device locked somewhere?");
			}
			
			NativeMethods.v4l2_format fmt = new NativeMethods.v4l2_format();
			fmt.type = V4L2_BUF_TYPE_SDR_CAPTURE;
			//fmt.fmt.sdr.pixelformat = V4L2_PIX_FMT_SDR_U8;
			fmt.fmt.sdr.pixelformat = V4L2_PIX_FMT_SDR_U16LE;
			Console.WriteLine("request fmt.pixelformat = {0}", fmt.fmt.sdr.pixelformat);
			
			//var v4l2_r = NativeMethods.v4l2_ioctl(_fd, CMD64_VIDIOC_S_FMT, ref fmt); 
			var v4l2_r = NativeMethods.ioctl(_fd, CMD64_VIDIOC_S_FMT, ref fmt); 
			Console.WriteLine("v4l2_ioctl r = {0} sdr.pixelformat = {1}", v4l2_r, fmt.fmt.sdr.pixelformat);

			if (fmt.fmt.sdr.pixelformat != V4L2_PIX_FMT_SDR_U8) {
				// throw exception?
				Console.WriteLine("fmt.fmt.sdr.pixelformat");
			}
		}

		public void Close()
		{
			Console.WriteLine("Close");
			//NativeMethods.v4l2_close(_fd);
			NativeMethods.close(_fd);
		}

		public void Start(SamplesAvailableDelegate callback)
		{
			int v4l2_r;
			Console.WriteLine("Start");
			Samplerate = _sampleRate; // set sampling rate
			
			NativeMethods.v4l2_requestbuffers req = new NativeMethods.v4l2_requestbuffers();
			req.count = 10; // nbuffers to driver
			req.type = V4L2_BUF_TYPE_SDR_CAPTURE;
			req.memory = V4L2_MEMORY_MMAP;
			//v4l2_r = NativeMethods.v4l2_ioctl(_fd, CMD64_VIDIOC_REQBUFS, ref req); 
			v4l2_r = NativeMethods.ioctl(_fd, CMD64_VIDIOC_REQBUFS, ref req); 
			Console.WriteLine("CMD64_VIDIOC_REQBUFS v4l2_ioctl r = {0} req.count = {1}", v4l2_r, req.count);
			
			
			// v4l2_mmap buffers
			mmap_start = new IntPtr [req.count];
			mmap_length = new UInt32 [req.count];
//			mmap = new Mmap [req.count];
			
			for (n_buffers = 0; n_buffers < req.count; n_buffers++) {
				buf.type = V4L2_BUF_TYPE_SDR_CAPTURE;
				buf.memory = V4L2_MEMORY_MMAP;
				buf.index = n_buffers;
				
				//v4l2_r = NativeMethods.v4l2_ioctl(_fd, CMD64_VIDIOC_QUERYBUF, ref buf); 
				v4l2_r = NativeMethods.ioctl(_fd, CMD64_VIDIOC_QUERYBUF, ref buf); 

//				mmap[n_buffers].length = buf.length;
//				mmap[n_buffers].start = NativeMethods.v4l2_mmap(IntPtr.Zero, buf.length, PROT_READ | PROT_WRITE, MAP_SHARED, _fd, buf.m.offset);
				mmap_length[n_buffers] = buf.length;
				//mmap_start[n_buffers] = NativeMethods.v4l2_mmap(IntPtr.Zero, buf.length, PROT_READ | PROT_WRITE, MAP_SHARED, _fd, buf.m.offset);
				mmap_start[n_buffers] = NativeMethods.mmap(IntPtr.Zero, buf.length, PROT_READ | PROT_WRITE, MAP_SHARED, _fd, buf.m.offset);
				Console.WriteLine("CMD64_VIDIOC_QUERYBUF v4l2_ioctl r = {0} mmap_start[n_buffers] = {1}", v4l2_r, mmap_start[n_buffers]);
			}
			
			//Exchange a buffer with the driver
			for (uint i = 0; i < n_buffers; i++) {
				//CLEAR(buf);
				buf.type = V4L2_BUF_TYPE_SDR_CAPTURE;
				buf.memory = V4L2_MEMORY_MMAP;
				buf.index = i;
				//v4l2_r = NativeMethods.v4l2_ioctl(_fd, CMD64_VIDIOC_QBUF, ref buf);
				v4l2_r = NativeMethods.ioctl(_fd, CMD64_VIDIOC_QBUF, ref buf);
				Console.WriteLine("CMD64_VIDIOC_QBUF v4l2_ioctl r = {0} buf.index = {1}", v4l2_r, buf.index);
			}

			// start streaming
			int type = V4L2_BUF_TYPE_SDR_CAPTURE;
			int *ptr = & type;
			//v4l2_r = NativeMethods.v4l2_ioctl(_fd, CMD64_VIDIOC_STREAMON, (IntPtr)ptr);
			v4l2_r = NativeMethods.ioctl(_fd, CMD64_VIDIOC_STREAMON, (IntPtr)ptr);
			Console.WriteLine("CMD64_VIDIOC_STREAMON v4l2_ioctl r = {0}", v4l2_r);
			
			_callback = callback;
			
			// set device parameters here
			
			streaming = true;

			_sampleThread = new Thread(ReceiveData);
			_sampleThread.Start();
		}

		public void Stop()
		{
			Console.WriteLine("Stop");
			int v4l2_r;
			int type = V4L2_BUF_TYPE_SDR_CAPTURE;
			int *ptr = & type;
			streaming = false;
			
			// stop streaming
			//v4l2_r = NativeMethods.v4l2_ioctl(_fd, CMD64_VIDIOC_STREAMOFF, (IntPtr)ptr); 
			v4l2_r = NativeMethods.ioctl(_fd, CMD64_VIDIOC_STREAMOFF, (IntPtr)ptr); 
			Console.WriteLine("v4l2_ioctl CMD64_VIDIOC_STREAMOFF r = {0}", v4l2_r);
			
			// v4l2_munmap buffers
			for (int i = (int) n_buffers - 1; i >= 0; i--) {
				//NativeMethods.v4l2_munmap(mmap_start[i], mmap_length[i]);
				NativeMethods.munmap(mmap_start[i], mmap_length[i]);
//				NativeMethods.v4l2_munmap(mmap[i].start, mmap[i].length);
				Console.WriteLine("v4l2_munmap = {0}", i);
			}

			if (_sampleThread != null)
			{
				_sampleThread.Join();
				_sampleThread = null;
			}
			
			_callback = null;
		}

		private void ReceiveData()
		{
			Console.WriteLine("ReceiveData");

			UInt16 *v4l2_buf;
			int v4l2_r;
			int sampleCount;
			Complex *iqBuffer;
			
			buf.type = V4L2_BUF_TYPE_SDR_CAPTURE;
			buf.memory = V4L2_MEMORY_MMAP;

			while (streaming)
			{
				// request mmap buf from Kernel (fd is blocking mode)
				//v4l2_r = NativeMethods.v4l2_ioctl(_fd, CMD64_VIDIOC_DQBUF, ref buf); 
				v4l2_r = NativeMethods.ioctl(_fd, CMD64_VIDIOC_DQBUF, ref buf); 
				if (v4l2_r != 0) {
					Console.WriteLine("v4l2_ioctl VIDIOC_DQBUF ret = {0}", v4l2_r);
				}
					
				// offer samples from Kernel to SDRsharp DSP
				// convert to I/Q Complex
				if (_callback != null)
				{
//					v4l2_buf = mmap[buf.index].start;
					v4l2_buf = (UInt16 *) mmap_start[buf.index];
					sampleCount = (int) buf.bytesused / 4; // 2 x 16bit
					iqBuffer = (Complex *) UnsafeBuffer.Create(sampleCount, sizeof(Complex));
					var iqPtr = iqBuffer;
					for (var i = 0; i < sampleCount; i++)
					{
						iqPtr->Imag = _lutPtr[*v4l2_buf++];
						iqPtr->Real = _lutPtr[*v4l2_buf++];
						iqPtr++;
					}
					_callback(this, iqBuffer, sampleCount);
				}

				// return mmap buf to Kernel
				//v4l2_r = NativeMethods.v4l2_ioctl(_fd, CMD64_VIDIOC_QBUF, ref buf);
				v4l2_r = NativeMethods.ioctl(_fd, CMD64_VIDIOC_QBUF, ref buf);
				if (v4l2_r != 0) {
					Console.WriteLine("v4l2_ioctl VIDIOC_QBUF ret = {0}", v4l2_r);
				}
			}
		}
	}
}
