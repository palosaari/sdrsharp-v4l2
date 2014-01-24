using System;
using System.Runtime.InteropServices;

namespace SDRSharp.V4L2
{
	public class NativeMethods
	{
		private const string LibV4L2 = "v4l2";
		private const string LibC = "c";

		// TODO: size 32?
		[StructLayout(LayoutKind.Explicit, Size = 32)]
		public unsafe struct v4l2_format_sdr {
			[FieldOffset(0)]
			public UInt32 pixelformat;
			[FieldOffset(4)]
			public fixed Byte reserved[28];
		}
		
		[StructLayout(LayoutKind.Sequential, Size = 32)]
		public struct v4l2_pix_format {
			public UInt32 width;
			public UInt32 height;
			public UInt32 pixelformat;
			public UInt32 field; // enum v4l2_field
			public UInt32 bytesperline; // for padding, zero if unused
			public UInt32 sizeimage;
			public UInt32 colorspace; // enum v4l2_colorspace
			public UInt32 priv; // private data, depends on pixelformat
		};
		
		[StructLayout(LayoutKind.Explicit)]
		public struct Union_v4l2_format {
			[FieldOffset(0)]
			public v4l2_pix_format pix;
			[FieldOffset(0)]
			public v4l2_format_sdr sdr;
		}
		
		[StructLayout(LayoutKind.Explicit, Size = 208)]
		public struct v4l2_format {
			[FieldOffset(0)]
			public UInt32 type;
			[FieldOffset(8)]
			public Union_v4l2_format fmt;
		};
		
		[StructLayout(LayoutKind.Sequential, Size = 20)]
		public unsafe struct v4l2_requestbuffers {
			public UInt32 count;
			public UInt32 type; // enum v4l2_buf_type
			public UInt32 memory; // enum v4l2_memory
			public fixed UInt32 reserved[2];
		};

		[StructLayout(LayoutKind.Explicit)]
		public struct Union_v4l2_buffer {
			[FieldOffset(0)]
			public UInt32 offset;
		}

		[StructLayout(LayoutKind.Explicit, Size = 88)]
		public struct v4l2_buffer {
			[FieldOffset(0)]
			public UInt32 index;
			[FieldOffset(4)]
			public UInt32 type;
			[FieldOffset(8)]
			public UInt32 bytesused;
			[FieldOffset(12)]
			public UInt32 flags;
			[FieldOffset(16)]
			public UInt32 field;
			// a lot of fields missing...
			[FieldOffset(60)]
			public UInt32 memory;
			[FieldOffset(64)]
			public Union_v4l2_buffer m;
			[FieldOffset(72)]
			public UInt32 length;
		};

		[StructLayout(LayoutKind.Explicit, Size = 20)]
		public unsafe struct v4l2_ext_control {
			[FieldOffset(0)]
			public UInt32 id;
			[FieldOffset(4)]
			public UInt32 size;
			[FieldOffset(8)]
			public fixed UInt32 reserved2[1];
			// nameless union
			[FieldOffset(12)]
			public Int32 value;
			[FieldOffset(12)]
			public Int64 value64;
		}

		[StructLayout(LayoutKind.Explicit, Size = 32)]
		public unsafe struct v4l2_ext_controls {
			[FieldOffset(0)]
			public UInt32 ctrl_class;
			[FieldOffset(4)]
			public UInt32 count;
			[FieldOffset(8)]
			public UInt32 error_idx;
			[FieldOffset(12)]
			public fixed UInt32 reserved[2];
			[FieldOffset(24)]
			public IntPtr controls; //struct v4l2_ext_control *controls;
		}

		[StructLayout(LayoutKind.Explicit, Size = 44)]
		public unsafe struct v4l2_frequency {
			[FieldOffset(0)]
			public UInt32 tuner;
			[FieldOffset(4)]
			public UInt32 type;	// enum v4l2_tuner_type
			[FieldOffset(8)]
			public UInt32 frequency;
			[FieldOffset(12)]
			public fixed UInt32 reserved[8];
		}
		
		// int open(const char *path, int oflag, ... );
		[DllImport(LibC, EntryPoint = "open", CallingConvention = CallingConvention.Cdecl)]
		public static extern int open(string file, int oflag);
		// int close(int fd);
		[DllImport(LibC, EntryPoint = "close", CallingConvention = CallingConvention.Cdecl)]
		public static extern int close(int fd);
		// void *mmap(void *addr, size_t length, int prot, int flags, int fd, off_t offset);
		[DllImport(LibC, EntryPoint = "mmap", CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr mmap(IntPtr addr, UInt32 length, int prot, int flags, int fd, Int64 offset);
		// int munmap(void *addr, size_t length);
		[DllImport(LibC, EntryPoint = "munmap", CallingConvention = CallingConvention.Cdecl)]
		public static extern int munmap(IntPtr addr, UInt32 length);
		// int ioctl(int fildes, int request, ... /* arg */);
		[DllImport(LibC, EntryPoint = "ioctl", CallingConvention = CallingConvention.Cdecl)]
		public static extern int ioctl(int fd, UInt64 request, ref v4l2_format fmt);
		[DllImport(LibC, EntryPoint = "ioctl", CallingConvention = CallingConvention.Cdecl)]
		public static extern int ioctl(int fd, UInt64 request, ref v4l2_requestbuffers req);
		[DllImport(LibC, EntryPoint = "ioctl", CallingConvention = CallingConvention.Cdecl)]
		public static extern int ioctl(int fd, UInt64 request, ref v4l2_buffer buf);
		[DllImport(LibC, EntryPoint = "ioctl", CallingConvention = CallingConvention.Cdecl)]
		public static extern int ioctl(int fd, UInt64 request, IntPtr type);
		[DllImport(LibC, EntryPoint = "ioctl", CallingConvention = CallingConvention.Cdecl)]
		public static extern int ioctl(int fd, UInt64 request, ref v4l2_ext_controls ext_ctrls);
		[DllImport(LibC, EntryPoint = "ioctl", CallingConvention = CallingConvention.Cdecl)]
		public static extern int ioctl(int fd, UInt64 request, ref v4l2_frequency frequency);

		/*
		// int v4l2_open(const char *file, int oflag, ...);
		[DllImport(LibV4L2, EntryPoint = "v4l2_open", CallingConvention = CallingConvention.Cdecl)]
		public static extern int v4l2_open(string file, int oflag);
		// int v4l2_close(int fd);
		[DllImport(LibV4L2, EntryPoint = "v4l2_close", CallingConvention = CallingConvention.Cdecl)]
		public static extern int v4l2_close(int fd);
		// int v4l2_dup(int fd);
		[DllImport(LibV4L2, EntryPoint = "v4l2_dup", CallingConvention = CallingConvention.Cdecl)]
		public static extern int v4l2_dup(int fd);
		// int v4l2_ioctl(int fd, unsigned long int request, ...);
		[DllImport(LibV4L2, EntryPoint = "v4l2_ioctl", CallingConvention = CallingConvention.Cdecl)]
		public static extern int v4l2_ioctl(int fd, UInt64 request, ref v4l2_format fmt);
		[DllImport(LibV4L2, EntryPoint = "v4l2_ioctl", CallingConvention = CallingConvention.Cdecl)]
		public static extern int v4l2_ioctl(int fd, UInt64 request, ref v4l2_requestbuffers req);
		[DllImport(LibV4L2, EntryPoint = "v4l2_ioctl", CallingConvention = CallingConvention.Cdecl)]
		public static extern int v4l2_ioctl(int fd, UInt64 request, ref v4l2_buffer buf);
		[DllImport(LibV4L2, EntryPoint = "v4l2_ioctl", CallingConvention = CallingConvention.Cdecl)]
		public static extern int v4l2_ioctl(int fd, UInt64 request, IntPtr type);
		[DllImport(LibV4L2, EntryPoint = "v4l2_ioctl", CallingConvention = CallingConvention.Cdecl)]
		public static extern int v4l2_ioctl(int fd, UInt64 request, ref v4l2_ext_controls ext_ctrls);
		[DllImport(LibV4L2, EntryPoint = "v4l2_ioctl", CallingConvention = CallingConvention.Cdecl)]
		public static extern int v4l2_ioctl(int fd, UInt64 request, ref v4l2_frequency frequency);
		// ssize_t v4l2_read(int fd, void *buffer, size_t n);
		[DllImport(LibV4L2, EntryPoint = "v4l2_read", CallingConvention = CallingConvention.Cdecl)]
		public static extern int v4l2_read(int fd, byte[] buffer, uint n);
		// void *v4l2_mmap(void *start, size_t length, int prot, int flags, int fd, int64_t offset);
		[DllImport(LibV4L2, EntryPoint = "v4l2_mmap", CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr v4l2_mmap(IntPtr start, UInt32 length, int prot, int flags, int fd, Int64 offset);
		// int v4l2_munmap(void *_start, size_t length);
		[DllImport(LibV4L2, EntryPoint = "v4l2_munmap", CallingConvention = CallingConvention.Cdecl)]
		public static extern int v4l2_munmap(IntPtr _start, UInt32 length);
		// int v4l2_set_control(int fd, int cid, int value);
		[DllImport(LibV4L2, EntryPoint = "v4l2_set_control", CallingConvention = CallingConvention.Cdecl)]
		public static extern int v4l2_set_control(int fd, int cid, int value_);
		// int v4l2_get_control(int fd, int cid);
		// int v4l2_fd_open(int fd, int v4l2_flags);
		*/
	}
}