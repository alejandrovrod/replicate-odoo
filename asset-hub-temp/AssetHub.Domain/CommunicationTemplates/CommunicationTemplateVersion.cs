using System;
using System.Collections.Generic;

namespace AssetHub.Domain.CommunicationTemplates;

public class CommunicationTemplateVersion
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public CommunicationTemplate? Template { get; set; }

    public int VersionNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CommunicationTemplateTranslation> Translations { get; set; } = new List<CommunicationTemplateTranslation>();
}
