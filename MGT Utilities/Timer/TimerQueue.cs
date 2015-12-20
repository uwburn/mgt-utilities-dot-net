using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace MGT.Utilities.Timer
{
    public class TimerQueue : IDisposable
    {
        public enum TimerQueueTimerFlags : uint
        {
            ExecuteDefault = 0x0000,
            ExecuteInTimerThread = 0x0020,
            ExecuteInIoThread = 0x0001,
            ExecuteInPersistentThread = 0x0080,
            ExecuteLongFunction = 0x0010,
            ExecuteOnlyOnce = 0x0008,
            TransferImpersonation = 0x0100,
        }

        public delegate void Win32WaitOrTimerCallback(IntPtr lpParam, [MarshalAs(UnmanagedType.U1)]bool bTimedOut);

        [DllImport("kernel32.dll", SetLastError = true)]
        public extern static IntPtr CreateTimerQueue();

        [DllImport("kernel32.dll", SetLastError = true)]
        public extern static bool DeleteTimerQueue(IntPtr timerQueue);

        [DllImport("kernel32.dll", SetLastError = true)]
        public extern static bool DeleteTimerQueueEx(IntPtr timerQueue, IntPtr completionEvent);

        [DllImport("kernel32.dll", SetLastError = true)]
        public extern static bool CreateTimerQueueTimer(
            out IntPtr newTimer,
            IntPtr timerQueue,
            Win32WaitOrTimerCallback callback,
            IntPtr userState,
            uint dueTime,
            uint period,
            TimerQueueTimerFlags flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        public extern static bool ChangeTimerQueueTimer(
            IntPtr timerQueue,
            ref IntPtr timer,
            uint dueTime,
            uint period);

        [DllImport("kernel32.dll", SetLastError = true)]
        public extern static bool DeleteTimerQueueTimer(
            IntPtr timerQueue,
            IntPtr timer,
            IntPtr completionEvent);

        private IntPtr timerQueue;
        private static Win32WaitOrTimerCallback callBack;
        IntPtr timerQueueTimer;
        IntPtr userState = IntPtr.Zero;

        public bool Running { get; private set; }
        private int interval = 1000;
        public int Interval
        {
            get
            {
                return interval;
            }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("Interval must be positive");

                if (Running)
                {
                    bool rslt = ChangeTimerQueueTimer(timerQueue, ref timerQueueTimer, 0, (uint)value);
                    if (!rslt)
                    {
                        int err = Marshal.GetLastWin32Error();
                        throw new Win32Exception(err, "Error changing the TimerQueue interval");
                    }
                }

                interval = value;
            }
        }

        public bool PersistentThread { get; set; }

        public delegate void TimerTickHandler(object sender, TimerQueueEventArgs e);
        public event TimerTickHandler OnTimerTick;

        public TimerQueue()
        {
            timerQueue = CreateTimerQueue();
            callBack = new Win32WaitOrTimerCallback(Tick);
            Running = false;
        }

        ~TimerQueue()
        {
            Dispose(false);
        }

        public void Start()
        {
            TimerQueueTimerFlags flags;
            if (PersistentThread)
                flags = TimerQueueTimerFlags.ExecuteInPersistentThread;
            else
                flags = TimerQueueTimerFlags.ExecuteDefault;
            bool rslt = CreateTimerQueueTimer(out timerQueueTimer, timerQueue, callBack, userState, 0, (uint)interval, flags);
            if (!rslt)
            {
                int err = Marshal.GetLastWin32Error();
                throw new Win32Exception(err, "Error starting TimerQueue");
            }
            else
            {
                Running = true;
            }
        }

        public void Stop()
        {
            if (!Running)
                return;

            bool rslt = DeleteTimerQueueTimer(timerQueue, timerQueueTimer, IntPtr.Zero);
            if (!rslt)
            {
                int err = Marshal.GetLastWin32Error();
                throw new Win32Exception(err, "Error stopping TimerQueue");
            }
            else
            {
                Running = false;
            }
        }

        private void Tick(IntPtr lpParam, [MarshalAs(UnmanagedType.U1)]bool bTimedOut)
        {
            if (OnTimerTick != null)
            {
                TimerQueueEventArgs args = new TimerQueueEventArgs();
                OnTimerTick(this, args);
            }
        }

        private bool Disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                Stop();
                bool rslt = DeleteTimerQueueEx(timerQueue, IntPtr.Zero);
                if (!rslt)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, "Error disposing TimerQueue");
                }

                Disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
