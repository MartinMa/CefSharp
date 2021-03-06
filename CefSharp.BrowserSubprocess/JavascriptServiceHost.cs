﻿using CefSharp.Internals.JavascriptBinding;
using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Description;

namespace CefSharp.BrowserSubprocess
{
    internal class JavascriptServiceHost
    {
        public static ServiceHost Create(int parentProcessId, int browserId)
        {
            // The setup of the WCF host here is a bit non-trivial because we must support multiple subprocesses (when there is
            // more than one WebView in the application, for example). Inspired by this SO post:
            // http://stackoverflow.com/questions/10362246/two-unique-named-pipes-conflicting-and-invalidcredentialexception
            var host = new ServiceHost(typeof(JavascriptProxy), new Uri[0]);
            AddDebugBehavior(host);

            var serviceName = JavascriptProxySupport.BaseAddress + "/" + JavascriptProxySupport.GetServiceName(parentProcessId, browserId);

            KillExistingServiceIfNeeded(serviceName);

            Kernel32.OutputDebugString("Setting up IJavascriptProxy using service name: " + serviceName);
            host.AddServiceEndpoint(
                typeof(IJavascriptProxy),
                new NetNamedPipeBinding(),
                new Uri(serviceName)
            );

            host.Open();
            return host;
        }

        private static void KillExistingServiceIfNeeded(string serviceName)
        {
            // It might be that there is an existing process already bound to this port. We must get rid of that one, so that the
            // endpoint address gets available for us to use.
            try
            {
                var channelFactory = new ChannelFactory<IJavascriptProxy>(
                    new NetNamedPipeBinding(),
                    new EndpointAddress(serviceName)
                    );
                channelFactory.Open(TimeSpan.FromSeconds(1));
                var javascriptProxy = channelFactory.CreateChannel();
                javascriptProxy.Terminate();
            }
            catch
            {
                // We assume errors at this point are caused by things like the endpoint not being present (which will happen in
                // the first render subprocess instance).
            }
        }

        private static void AddDebugBehavior(ServiceHostBase host)
        {
            var serviceDebugBehavior = host.Description.Behaviors.Find<ServiceDebugBehavior>();

            if (serviceDebugBehavior == null)
            {
                serviceDebugBehavior = new ServiceDebugBehavior
                {
                    IncludeExceptionDetailInFaults = true
                };
                host.Description.Behaviors.Add(serviceDebugBehavior);
            }
            else
            {
                serviceDebugBehavior.IncludeExceptionDetailInFaults = true;
            }
        }
    }
}