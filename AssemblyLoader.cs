using System;
using System.Linq;
using System.IO;
using System.Reflection;


namespace ASTAWebServer
{
    internal static class AssemblyLoader
    {
        //https://stackoverrun.com/ru/q/4794740
        internal static void RegisterAssemblyLoader()
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve -= OnResolveAssembly;
            currentDomain.AssemblyResolve += OnResolveAssembly;
        }

        private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            return LoadAssemblyFromManifest(args.Name);
        }

        private static Assembly LoadAssemblyFromManifest(string targetAssemblyName)
        {
         //   Logger.WriteString("targetAssemblyName: " + targetAssemblyName);
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            byte[] assemblyRawBytes = null;

            //var names = typeof(Program).Assembly.GetManifestResourceNames();
            //foreach (var n in names)
            //{
            //    log.WriteString("n: " + n);
            //}

            try
            {
                AssemblyName assemblyName = new AssemblyName(targetAssemblyName);

                string resourceName = DetermineEmbeddedResourceName(assemblyName, executingAssembly);

                using (Stream stream = executingAssembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                //        Logger.WriteString($"length = 0. {resourceName} not found");
                        return null;
                    }

                    assemblyRawBytes = new byte[stream.Length];
                    stream.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);

             //       Logger.WriteString($"{resourceName} loaded");
                }
            }
            catch (Exception err) { 
            //    Logger.WriteString($"err: {err.ToString()}"); 
            }

            return Assembly.Load(assemblyRawBytes);

            //Assembly executingAssembly = Assembly.GetExecutingAssembly();
            //AssemblyName assemblyName = new AssemblyName(targetAssemblyName);

            //string resourceName = DetermineEmbeddedResourceName(assemblyName, executingAssembly);

            //using (Stream stream = executingAssembly.GetManifestResourceStream(resourceName))
            //{
            //    if (stream == null)
            //        return null;

            //    using (var deflated = new DeflateStream(stream, CompressionMode.Decompress))
            //    using (var reader = new BinaryReader(deflated))
            //    {
            //        var one_megabyte = 1024 * 1024;
            //        var buffer = reader.ReadBytes(one_megabyte);
            //        return Assembly.Load(buffer);
            //    }
            //}

        }

        private static string DetermineEmbeddedResourceName(AssemblyName assemblyName, Assembly executingAssembly)
        {
            //This assumes you have the assemblies in a folder named "Resources"
            string resourceName = $"{executingAssembly.GetName().Name}.Resources.{assemblyName.Name}.dll";

            //This logic finds the assembly manifest name even if it's not an case match for the requested assembly                          
            var matchingResource = executingAssembly
                .GetManifestResourceNames()
                .FirstOrDefault(res => res.ToLower() == resourceName.ToLower());

            if (matchingResource != null)
            {
                resourceName = matchingResource;
            }
            return resourceName;
        }
    }
}
