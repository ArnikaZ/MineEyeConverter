using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;
using Topshelf.Logging;

namespace ConsoleApp9
{
    internal class SampleService : ServiceControl
    {
        ModbusGateway gateway;
        string instanceName;
        readonly bool _throwOnStart;
        readonly string _throwOnStartValue;
        readonly bool _throwOnStop;
        readonly bool _throwUnhandled;
        static readonly LogWriter _log = HostLogger.Get<SampleService>();

        public SampleService(bool throwOnStart, string throwOnStartValue, bool throwOnStop, bool throwUnhandled)
        {
            _throwOnStart = throwOnStart;
            _throwOnStartValue = throwOnStartValue;
            _throwOnStop = throwOnStop;
            _throwUnhandled = throwUnhandled;
        }

        public bool Start(HostControl hostControl)
        {
            _log.Info("SampleService Starting...");
            hostControl.RequestAdditionalTime(TimeSpan.FromSeconds(10));
            Thread.Sleep(1000);
            gateway = new ModbusGateway("Przenosnik2");
            gateway.Start();
            if (_throwOnStart)
            {
                if (!string.IsNullOrEmpty(_throwOnStartValue))
                {
                    _log.Info($"Received argument for throwonstart: {_throwOnStartValue}");
                    // Tutaj możesz wykorzystać _throwOnStartValue według potrzeb
                }
                else
                {
                    _log.Info("Throwing as requested");
                    throw new InvalidOperationException("Throw on Start Requested");
                }
            }

            ThreadPool.QueueUserWorkItem(x =>
            {
                Thread.Sleep(3000);
                if (_throwUnhandled)
                    throw new InvalidOperationException("Throw Unhandled In Random Thread");

                _log.Info("Requesting stop");
                hostControl.Stop();
            });
            _log.Info("SampleService Started");
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            _log.Info("SampleService Stopped");
            if (_throwOnStop)
                throw new InvalidOperationException("Throw on Stop Requested!");
            return true;
        }

        public bool Pause(HostControl hostControl)
        {
            _log.Info("SampleService Paused");
            return true;
        }

        public bool Continue(HostControl hostControl)
        {
            _log.Info("SampleService Continued");
            return true;
        }
    }
    
}
