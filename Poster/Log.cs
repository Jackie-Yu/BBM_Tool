using System;
using System.IO;
using System.Configuration;
using System.Timers;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;

namespace Achievo.Poster
{
    public static class Log
    {
        #region Cleanup log timer

        private static System.Timers.Timer _timer;

        static Log()
        {

        }

        #endregion

        #region Log Write
     

        /// <summary>
        /// write log
        /// </summary>
        /// <param name="msg">the log message to be logged.</param>
        public static void WriteLog(string msg)
        {
            try
            {
            
                string logFile = AppDomain.CurrentDomain.BaseDirectory + "log." + DateTime.Now.Date.ToString("yyyyMMdd") + ".txt";
                Stream stream = File.Open(logFile, FileMode.Append);
                StreamWriter sw = new StreamWriter(stream);

                try
                {
                    sw.WriteLine(String.Format("{0}___{1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), msg));
                }
                catch 
                {
                }
                finally
                {
                    if(sw != null)
                        sw.Close();
                }
            }
            catch 
            { }
        }

        #endregion

   
    }
}