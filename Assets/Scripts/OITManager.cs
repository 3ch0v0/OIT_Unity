using System.Collections.Generic;

public enum OITAlgorithm
{
    WBOIT,
    DepthPeeling,
    Abuffer,
    DFAOIT,
    Kbuffer,
}

public static class OITRegistry
{
    public static int Layers = 4;
    
    public static readonly Dictionary<OITAlgorithm, HashSet<OITObject>> Objects = new Dictionary<OITAlgorithm, HashSet<OITObject>>()
    {
        
        { OITAlgorithm.WBOIT, new HashSet<OITObject>() },
        { OITAlgorithm.DepthPeeling, new HashSet<OITObject>() },
        { OITAlgorithm.Abuffer, new HashSet<OITObject>() },
        { OITAlgorithm.DFAOIT, new HashSet<OITObject>() },
        { OITAlgorithm.Kbuffer, new HashSet<OITObject>() },
    };
}