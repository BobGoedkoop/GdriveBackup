using System;
using GDriveBackup.Controller;

// ReSharper disable StringLiteralTypo

namespace GDriveBackup
{
    class Program
    {
        static void Main( string[] args )
        {
            var mainController = new MainController();
            mainController.Execute( args );
        }
    }
}
