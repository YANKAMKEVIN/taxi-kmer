using System.Runtime.CompilerServices;

// Les tests EditMode de Unity vivent dans leur propre assembly (TaxiKmer.Core.Tests) ;
// hors Unity (tools/dotnet), logique et tests sont compilés ensemble et n'en ont pas besoin.
[assembly: InternalsVisibleTo("TaxiKmer.Core.Tests")]
[assembly: InternalsVisibleTo("TaxiKmer.PlayMode.Tests")]
