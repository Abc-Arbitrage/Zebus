using System.Reflection;

[assembly: AssemblyProduct("Zebus.Directory")]
[assembly: AssemblyDescription("The Directory service used by Zebus")]
[assembly: AssemblyCompany("ABC Arbitrage Asset Management")]
[assembly: AssemblyCopyright("Copyright © ABC Arbitrage Asset Management 2014")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif