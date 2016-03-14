using System.Reflection;

[assembly: AssemblyProduct("Zebus.Persistence")]
[assembly: AssemblyDescription("The Persistence service used by Zebus")]
[assembly: AssemblyCompany("ABC Arbitrage Asset Management")]
[assembly: AssemblyCopyright("Copyright © ABC Arbitrage Asset Management 2016")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif