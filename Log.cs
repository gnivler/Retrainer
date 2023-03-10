using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Retrainer {
  public static class Log {
    //private static string m_assemblyFile;
    private static string m_logfile;
    private static readonly Mutex mutex = new Mutex();
    public static string BaseDirectory;
    private static StringBuilder m_cache = new StringBuilder();
    private static StreamWriter m_fs = null;
    public static void InitLog() {
      Log.m_logfile = Path.Combine(BaseDirectory, "Log.txt");
      File.Delete(Log.m_logfile);
      Log.m_fs = new StreamWriter(Log.m_logfile);
      Log.m_fs.AutoFlush = true;
    }
    public static void flush() {
      if (Log.mutex.WaitOne(1000)) {
        Log.m_fs.Write(Log.m_cache.ToString());
        Log.m_fs.Flush();
        Log.m_cache.Length = 0;
        Log.mutex.ReleaseMutex();
      }
    }
    public static void LogWrite(int initiation, string line, bool eol = false, bool timestamp = false) {
      string init = new string(' ', initiation);
      string prefix = String.Empty;
      if (timestamp) { prefix = DateTime.Now.ToString("[HH:mm:ss.fff]"); }
      if (initiation > 0) { prefix += init; };
      if (eol) {
        LogWrite(prefix + line + "\n");
      } else {
        LogWrite(prefix + line);
      }
    }
    public static void LogWrite(string line) {
      m_cache.Append(line);
      Log.flush();
    }
    public static void W(string line) {
      LogWrite(line);
    }
    public static void WL(string line) {
      line += "\n"; W(line);
    }
    public static void W(int initiation, string line) {
      string init = new string(' ', initiation);
      line = init + line; W(line);
    }
    public static void WL(int initiation, string line) {
      string init = new string(' ', initiation);
      line = init + line; WL(line);
    }
    public static void TW(int initiation, string line) {
      string init = new string(' ', initiation);
      line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "]" + init + line;
      W(line);
    }
    public static void TWL(int initiation, string line) {
      string init = new string(' ', initiation);
      line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "]" + init + line;
      WL(line);
    }
  }
}