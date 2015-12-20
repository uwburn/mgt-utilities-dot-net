using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MGT.Utilities.Timer
{
    public sealed class MultimediaTimer : IComponent
    {
        // Represents the method that is called by Windows when a timer event occurs.
        private delegate void TimeProc(int id, int msg, int user, int param1, int param2);

        // Represents methods that raise events.
        private delegate void EventRaiser(EventArgs e);

        // Gets timer capabilities.
        [DllImport("winmm.dll")]
        private static extern int timeGetDevCaps(ref MultimediaTimerCaps caps, int sizeOfTimerCaps);

        // Creates and starts the timer.
        [DllImport("winmm.dll")]
        private static extern int timeSetEvent(int delay, int resolution, TimeProc proc, int user, int mode);

        // Stops and destroys the timer.
        [DllImport("winmm.dll")]
        private static extern int timeKillEvent(int id);

        // Indicates that the operation was successful.
        private const int TIMERR_NOERROR = 0;

        // Timer identifier.
        private int timerID;

        // Timer mode.
        private volatile MultimediaTimerMode mode;

        // Period between timer events in milliseconds.
        private volatile int period;

        // Timer resolution in milliseconds.
        private volatile int resolution;        

        // Called by Windows when a timer periodic event occurs.
        private TimeProc timeProcPeriodic;

        // Called by Windows when a timer one shot event occurs.
        private TimeProc timeProcOneShot;

        // Represents the method that raises the Tick event.
        private EventRaiser tickRaiser;

        // Indicates whether or not the timer is running.
        private bool running = false;

        // Indicates whether or not the timer has been disposed.
        private volatile bool disposed = false;

        // The ISynchronizeInvoke object to use for marshaling events.
        private ISynchronizeInvoke synchronizingObject = null;

        // For implementing IComponent.
        private ISite site = null;

        // Multimedia timer capabilities.
        private static MultimediaTimerCaps caps;

        /// <summary>
        /// Occurs when the Timer has started;
        /// </summary>
        public event EventHandler Started;

        /// <summary>
        /// Occurs when the Timer has stopped;
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Occurs when the time period has elapsed.
        /// </summary>
        public event EventHandler Tick;

        /// <summary>
        /// Initialize class.
        /// </summary>
        static MultimediaTimer()
        {
            // Get multimedia timer capabilities.
            timeGetDevCaps(ref caps, Marshal.SizeOf(caps));
        }

        /// <summary>
        /// Initializes a new instance of the Timer class with the specified IContainer.
        /// </summary>
        /// <param name="container">
        /// The IContainer to which the Timer will add itself.
        /// </param>
        public MultimediaTimer(IContainer container)
        {
            ///
            /// Required for Windows.Forms Class Composition Designer support
            ///
            container.Add(this);

            Initialize();
        }

        /// <summary>
        /// Initializes a new instance of the Timer class.
        /// </summary>
        public MultimediaTimer()
        {
            Initialize();
        }

        ~MultimediaTimer()
        {
            if(IsRunning)
            {
                // Stop and destroy timer.
                timeKillEvent(timerID);
            }
        }

        // Initialize timer with default values.
        private void Initialize()
        {
            this.mode = MultimediaTimerMode.Periodic;
            this.period = Capabilities.periodMin;
            this.resolution = 1;

            running = false;

            timeProcPeriodic = new TimeProc(TimerPeriodicEventCallback);
            timeProcOneShot = new TimeProc(TimerOneShotEventCallback);
            tickRaiser = new EventRaiser(OnTick);
        }

        /// <summary>
        /// Starts the timer.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The timer has already been disposed.
        /// </exception>
        /// <exception cref="MultimediaTimerStartException">
        /// The timer failed to start.
        /// </exception>
        public void Start()
        {
            if(disposed)
            {
                throw new ObjectDisposedException("Timer");
            }

            if(IsRunning)
            {
                return;
            }

            // If the periodic event callback should be used.
            if(Mode == MultimediaTimerMode.Periodic)
            {
                // Create and start timer.
                timerID = timeSetEvent(Period, Resolution, timeProcPeriodic, 0, (int)Mode);
            }
            // Else the one shot event callback should be used.
            else
            {
                // Create and start timer.
                timerID = timeSetEvent(Period, Resolution, timeProcOneShot, 0, (int)Mode);
            }

            // If the timer was created successfully.
            if(timerID != 0)
            {
                running = true;

                if(SynchronizingObject != null && SynchronizingObject.InvokeRequired)
                {
                    SynchronizingObject.BeginInvoke(
                        new EventRaiser(OnStarted), 
                        new object[] { EventArgs.Empty });
                }
                else
                {
                    OnStarted(EventArgs.Empty);
                }                
            }
            else
            {
                throw new MultimediaTimerStartException("Unable to start multimedia Timer");
            }
        }

        /// <summary>
        /// Stops timer.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// If the timer has already been disposed.
        /// </exception>
        public void Stop()
        {
            if(disposed)
            {
                throw new ObjectDisposedException("Timer");
            }

            if(!running)
            {
                return;
            }

            // Stop and destroy timer.
            int result = timeKillEvent(timerID);

            Debug.Assert(result == TIMERR_NOERROR);

            running = false;

            if(SynchronizingObject != null && SynchronizingObject.InvokeRequired)
            {
                SynchronizingObject.BeginInvoke(
                    new EventRaiser(OnStopped), 
                    new object[] { EventArgs.Empty });
            }
            else
            {
                OnStopped(EventArgs.Empty);
            }
        }        

        // Callback method called by the Win32 multimedia timer when a timer
        // periodic event occurs.
        private void TimerPeriodicEventCallback(int id, int msg, int user, int param1, int param2)
        {
            if(synchronizingObject != null)
            {
                synchronizingObject.BeginInvoke(tickRaiser, new object[] { EventArgs.Empty });
            }
            else
            {
                OnTick(EventArgs.Empty);
            }
        }

        // Callback method called by the Win32 multimedia timer when a timer
        // one shot event occurs.
        private void TimerOneShotEventCallback(int id, int msg, int user, int param1, int param2)
        {
            if(synchronizingObject != null)
            {
                synchronizingObject.BeginInvoke(tickRaiser, new object[] { EventArgs.Empty });
                Stop();
            }
            else
            {
                OnTick(EventArgs.Empty);
                Stop();
            }
        }

        // Raises the Disposed event.
        private void OnDisposed(EventArgs e)
        {
            EventHandler handler = Disposed;

            if(handler != null)
            {
                handler(this, e);
            }
        }

        // Raises the Started event.
        private void OnStarted(EventArgs e)
        {
            EventHandler handler = Started;

            if(handler != null)
            {
                handler(this, e);
            }
        }

        // Raises the Stopped event.
        private void OnStopped(EventArgs e)
        {
            EventHandler handler = Stopped;

            if(handler != null)
            {
                handler(this, e);
            }
        }

        // Raises the Tick event.
        private void OnTick(EventArgs e)
        {
            EventHandler handler = Tick;

            if(handler != null)
            {
                handler(this, e);
            }
        }       

        /// <summary>
        /// Gets or sets the object used to marshal event-handler calls.
        /// </summary>
        public ISynchronizeInvoke SynchronizingObject
        {
            get
            {
                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }

                return synchronizingObject;
            }
            set
            {
                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }

                synchronizingObject = value;
            }
        }

        /// <summary>
        /// Gets or sets the time between Tick events.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// If the timer has already been disposed.
        /// </exception>   
        public int Period
        {
            get
            {
                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }

                return period;
            }
            set
            {
                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }
                else if(value < Capabilities.periodMin || value > Capabilities.periodMax)
                {
                    throw new ArgumentOutOfRangeException("Period", value,
                        "Multimedia Timer period out of range");
                }

                period = value;

                if(IsRunning)
                {
                    Stop();
                    Start();
                }
            }
        }

        /// <summary>
        /// Gets or sets the timer resolution.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// If the timer has already been disposed.
        /// </exception>        
        /// <remarks>
        /// The resolution is in milliseconds. The resolution increases 
        /// with smaller values; a resolution of 0 indicates periodic events 
        /// should occur with the greatest possible accuracy. To reduce system 
        /// overhead, however, you should use the maximum value appropriate 
        /// for your application.
        /// </remarks>
        public int Resolution
        {
            get
            {
                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }

                return resolution;
            }
            set
            {
                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }
                else if(value < 0)
                {
                    throw new ArgumentOutOfRangeException("Resolution", value,
                        "Multimedia timer resolution out of range");
                }

                resolution = value;

                if(IsRunning)
                {
                    Stop();
                    Start();
                }
            }
        }

        /// <summary>
        /// Gets the timer mode.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// If the timer has already been disposed.
        /// </exception>
        public MultimediaTimerMode Mode
        {
            get
            {
                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }

                return mode;
            }
            set
            {
                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }
                
                mode = value;

                if(IsRunning)
                {
                    Stop();
                    Start();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the Timer is running.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return running;
            }
        }

        /// <summary>
        /// Gets the timer capabilities.
        /// </summary>
        public static MultimediaTimerCaps Capabilities
        {
            get
            {
                return caps;
            }
        }

        public event System.EventHandler Disposed;

        public ISite Site
        {
            get
            {
                return site;
            }
            set
            {
                site = value;
            }
        }

        /// <summary>
        /// Frees timer resources.
        /// </summary>
        public void Dispose()
        {
            if(disposed)
            {
                return;
            }             

            if(IsRunning)
            {
                Stop();
            }

            disposed = true;

            OnDisposed(EventArgs.Empty);
        }      
    }
}
