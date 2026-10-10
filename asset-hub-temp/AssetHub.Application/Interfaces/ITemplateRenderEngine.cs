namespace AssetHub.Application.Interfaces;

public interface ITemplateRenderEngine
{
    /// <summary>
    /// Renderiza una plantilla Scriban reemplazando las variables desconocidas por string vacio.
    /// </summary>
    string Render(string templateContent, IReadOnlyDictionary<string, object> variables);
}
