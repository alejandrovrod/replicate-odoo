using System;
using System.Collections.Generic;
using AssetHub.Application.Interfaces;
using Scriban;
using Scriban.Runtime;

namespace AssetHub.Infrastructure.Services.Templates;

/// <summary>
/// Motor de render basado en Scriban. Las claves punteadas del catalogo
/// (ej. "incident.title") se registran como objetos anidados para que la
/// sintaxis {{incident.title}} resuelva naturalmente. Variables desconocidas
/// renderizan como string vacio, sin error (REQ-002).
/// </summary>
public class ScribanTemplateRenderEngine : ITemplateRenderEngine
{
    public string Render(string templateContent, IReadOnlyDictionary<string, object> variables)
    {
        if (string.IsNullOrWhiteSpace(templateContent))
        {
            return string.Empty;
        }

        var template = Template.Parse(templateContent);
        if (template.HasErrors)
        {
            // Plantilla malformada: se devuelve el contenido sin renderizar
            // para no romper el envio del correo.
            return templateContent;
        }

        var root = new ScriptObject();
        foreach (var (key, value) in variables)
        {
            var leafValue = value == null ? string.Empty : value;
            AssignNested(root, key, leafValue);
        }

        var context = new TemplateContext
        {
            LoopLimit = 0,
            // Variables/miembros inexistentes => null => string vacio, sin excepcion.
            // RelaxedTarget permite {{a.b}} cuando "a" no existe (a es null).
            StrictVariables = false,
            EnableRelaxedTargetAccess = true,
            EnableRelaxedMemberAccess = true,
            EnableRelaxedIndexerAccess = true
        };
        context.PushGlobal(root);

        try
        {
            return template.Render(context) ?? string.Empty;
        }
        catch (Scriban.Syntax.ScriptRuntimeException)
        {
            // Error en runtime (ej. sintaxis ambigua): devolver el contenido
            // original para no romper el envio de la comunicacion.
            return templateContent;
        }
    }

    /// <summary>
    /// Registra "a.b.c" = valor como root.a.b.c (objetos anidados).
    /// </summary>
    private static void AssignNested(ScriptObject root, string dottedKey, object value)
    {
        var parts = dottedKey.Split('.');
        ScriptObject current = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            if (current[part] is ScriptObject nested)
            {
                current = nested;
            }
            else
            {
                var created = new ScriptObject();
                current[part] = created;
                current = created;
            }
        }
        current[parts[^1]] = value;
    }
}
