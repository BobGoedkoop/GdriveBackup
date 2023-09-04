using System;
using GDriveBackup.ApplicationLayer.Controller;

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
   