using System;

// ReSharper disable UnusedMember.Local

namespace GDriveBackup.BusinessLayer.Domain.CommandLineAdapter
{
    public class CommandLineParser
    {

        private readonly string[] _args;

        private int GetIndex(string value)
        {
            int pos = Array.IndexOf(this._args, value);

            if (pos > -1)
            {
                // the array contains the string and the pos variable will have its position in the array
            }
            return pos;
        }

        protected CommandLineParser(string[] args)
        {
            this._args = args;
        }

        public static CommandLineParser Parse(string[] args)
        {
            return new CommandLineParser(args);
        }

        public string[] Args => this._args;

        public bool HasArguments()
        {
            return this._args.Length > 0;
        }
        public bool HasCommand( string command )
        {
            var has = false;
            int pos = Array.IndexOf( this._args, $"-{command}");

            if (pos > -1)
            {
                has = true;
            }
            return has;
        }

        public string GetCommand(string command)
        {
            var cmd = string.Empty;
            int pos = Array.IndexOf(this._args, $"-{command}");

            if (pos > -1)
            {
                cmd = this._args[ pos + 1];
            }
            return cmd;

        }

        public string GetCommandValue( string command )
        {
            throw new NotImplementedException();
        }

    }
}