using System.Collections.Generic;

namespace StrapRevit.Core.IFC
{
    /// <summary>
    /// Modelo IFC parseado
    /// </summary>
    public class IFCModel
    {
        public string ProjectName { get; set; }
        public string ProjectDescription { get; set; }
        
        public List<IFCElement> Elements { get; set; } = new List<IFCElement>();
        public List<IFCBeam> Beams { get; set; } = new List<IFCBeam>();
        public List<IFCColumn> Columns { get; set; } = new List<IFCColumn>();
        public List<IFCSlab> Slabs { get; set; } = new List<IFCSlab>();
        public List<IFCFoundation> Foundations { get; set; } = new List<IFCFoundation>();
        
        public List<LevelInfo> RequiredLevels { get; set; } = new List<LevelInfo>();
        public List<string> RequiredMaterials { get; set; } = new List<string>();
    }

    /// <summary>
    /// Elemento IFC base
    /// </summary>
    public abstract class IFCElement
    {
        public string GlobalId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string TypeName { get; set; }
        public string Material { get; set; }
        public string Level { get; set; }
        public double Rotation { get; set; }
    }

    /// <summary>
    /// Viga IFC
    /// </summary>
    public class IFCBeam : IFCElement
    {
        public IFCPoint StartPoint { get; set; }
        public IFCPoint EndPoint { get; set; }
        public BeamProfile Profile { get; set; }
        public double Elevation { get; set; }
    }

    /// <summary>
    /// Pilar IFC
    /// </summary>
    public class IFCColumn : IFCElement
    {
        public IFCPoint Location { get; set; }
        public ColumnProfile Profile { get; set; }
        public string BaseLevel { get; set; }
        public string TopLevel { get; set; }
        public double BaseOffset { get; set; }
        public double TopOffset { get; set; }
    }

    /// <summary>
    /// Laje IFC
    /// </summary>
    public class IFCSlab : IFCElement
    {
        public List<IFCPoint> BoundaryPoints { get; set; } = new List<IFCPoint>();
        public double Thickness { get; set; }
        public double Elevation { get; set; }
        public double Offset { get; set; }
    }

    /// <summary>
    /// Fundação IFC
    /// </summary>
    public class IFCFoundation : IFCElement
    {
        public FoundationType FoundationType { get; set; }
        public IFCPoint Location { get; set; }
        public IFCPoint Dimensions { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public List<IFCCurve> Path { get; set; } = new List<IFCCurve>();
        public List<IFCCurve> Boundary { get; set; } = new List<IFCCurve>();
    }

    /// <summary>
    /// Ponto 3D
    /// </summary>
    public class IFCPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    /// <summary>
    /// Curva IFC
    /// </summary>
    public class IFCCurve
    {
        public bool IsLine { get; set; } = true;
        public bool IsArc { get; set; }
        public IFCPoint StartPoint { get; set; }
        public IFCPoint EndPoint { get; set; }
        public IFCPoint MidPoint { get; set; }
    }

    /// <summary>
    /// Perfil de viga
    /// </summary>
    public class BeamProfile
    {
        public string Type { get; set; } // Rectangular, I, T, L, Circular
        public double Width { get; set; }
        public double Height { get; set; }
        public double WebThickness { get; set; }
        public double FlangeThickness { get; set; }
    }

    /// <summary>
    /// Perfil de pilar
    /// </summary>
    public class ColumnProfile
    {
        public string Type { get; set; } // Rectangular, Circular, Custom
        public double Width { get; set; }
        public double Height { get; set; }
        public double Diameter { get; set; }
    }

    /// <summary>
    /// Tipo de fundação
    /// </summary>
    public enum FoundationType
    {
        Isolated,   // Sapata isolada
        Strip,      // Sapata corrida
        Mat         // Radier
    }

    /// <summary>
    /// Informação de nível
    /// </summary>
    public class LevelInfo
    {
        public string Name { get; set; }
        public double Elevation { get; set; }
    }
}

