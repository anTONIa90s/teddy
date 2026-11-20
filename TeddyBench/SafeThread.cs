using System;
using System.Threading;

namespace TeddyBench
{
    internal class SafeThread
    {
        private ThreadStart ThreadStart;
        private Thread Thread;

        public SafeThread(ThreadStart start, string name)
        {
            ThreadStart = start;
            Thread = new Thread(ThreadMain);
            Thread.Name = name;
            /* do not keep the process (and its handles on the SD card) alive after the main window closed */
            Thread.IsBackground = true;
        }

        private void ThreadMain()
        {
            try
            {
                ThreadStart.Invoke();
            }
            catch (ThreadAbortException ex)
            {
            }
            catch (Exception ex)
            {
                Program.MainClass.ReportException(Thread.Name, ex);
            }
        }

        internal void Start()
        {
            Thread.Start();
        }

        internal void Abort()
        {
            Thread.Abort();
        }

        internal bool Join(int v)
        {
            return Thread.Join(v);
        }
    }
}
