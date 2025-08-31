using System.Collections.Generic;

namespace ABD_Project.Models
{
    public class DiagramResultViewModel
    {
        public string NombreBD { get; set; } = string.Empty;
        public string MermaidEr { get; set; } = string.Empty;
        public string? MermaidEer { get; set; }
        public List<JerarquiaEer> JerarquiasAmbiguas { get; set; } = new();
    }
}
