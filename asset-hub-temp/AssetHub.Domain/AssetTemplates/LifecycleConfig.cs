using System.Collections.Generic;

namespace AssetHub.Domain.AssetTemplates;

public class LifecycleConfig
{
    public string InitialState { get; set; } = string.Empty;
    public Dictionary<string, List<string>> Transitions { get; set; } = new();
    
    // Configuración avanzada de estados (Colores, roles, campos requeridos)
    public Dictionary<string, StateConfig> States { get; set; } = new();

    // UI metadata to preserve visual canvas state (ReactFlow)
    public System.Text.Json.JsonElement? Nodes { get; set; }
    public System.Text.Json.JsonElement? Edges { get; set; }
}

public class StateConfig
{
    public string Color { get; set; } = "#94a3b8"; // Default slate-400
    public string Icon { get; set; } = string.Empty;
    public List<string> AllowedRoles { get; set; } = new();
    public List<string> RequiresFields { get; set; } = new();
    public string OnEnterAction { get; set; } = string.Empty;
    public string NotificationTargetFieldId { get; set; } = string.Empty;
    public int? MaxHoursInState { get; set; }
    public bool IsTerminal { get; set; } = false;
    public string AssociatedModule { get; set; } = string.Empty;
    public List<ChildStateDependency> ChildStateDependencies { get; set; } = new();
}

public class ChildStateDependency
{
    // "Any" or "All"
    public string ConditionType { get; set; } = "Any"; 
    
    // Lista de estados que el hijo(s) debe tener para cumplir la condición
    public List<string> ChildStates { get; set; } = new();
    
    // Estado al que debe transicionar el padre si se cumple
    public string TargetState { get; set; } = string.Empty;
}
