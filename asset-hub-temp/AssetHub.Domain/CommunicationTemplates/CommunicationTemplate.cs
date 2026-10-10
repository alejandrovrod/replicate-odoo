using System;

namespace AssetHub.Domain.CommunicationTemplates;

public enum CommunicationEntityScope
{
    Incident,
    MaintenanceOrder,
    WorkTask,
    Asset
}

public enum CommunicationTemplateType
{
    Email,
    Document
}

public class CommunicationTemplate
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    // Codigo unico por tenant, ej. "ORDER-CREATED", "TASK-CREATED"
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public CommunicationEntityScope EntityScope { get; set; }
    public CommunicationTemplateType TemplateType { get; set; }

    // Referencia a la version activa (null hasta tener al menos una version)
    public Guid? ActiveVersionId { get; set; }
    public CommunicationTemplateVersion? ActiveVersion { get; set; }

    public ICollection<CommunicationTemplateVersion> Versions { get; set; } = new List<CommunicationTemplateVersion>();
}
