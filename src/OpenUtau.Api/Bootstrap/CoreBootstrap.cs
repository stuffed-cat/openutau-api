using System;
using System.Reflection;
using OpenUtau.Core;

namespace OpenUtau.Api.Bootstrap
{
    public static class CoreBootstrap
    {
        public static void EnsurePhonemizerFactoriesLoaded()
        {
            var factories = PhonemizerFactory.GetAll();
            var property = typeof(DocManager).GetProperty("PhonemizerFactories", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("DocManager.PhonemizerFactories property was not found.");
            property.SetValue(DocManager.Inst, factories);
        }
    }
}