using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ZenWindowMover
{
    public static class Program
    {
        public static ZenWindowMover AppMainForm { get; private set; }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppMainForm = new ZenWindowMover();
            Application.Run(AppMainForm);
        }
    }
}
