using System.Windows;
using System.Windows.Controls;
using PowerMonitorDashboard.ViewModels;

namespace PowerMonitorDashboard.Controls;

/// <summary>
/// 根据节点类型选择拓扑图模板：发电机组、变压器、变电站、负荷使用不同的图形语义。
/// </summary>
public sealed class TopologyNodeTemplateSelector : DataTemplateSelector
{
    /// <inheritdoc />
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not TopologyNode node || container is not FrameworkElement element)
        {
            return null;
        }

        var key = node.Kind switch
        {
            TopologyNodeKind.Generator => "GeneratorNodeTemplate",
            TopologyNodeKind.Transformer => "TransformerNodeTemplate",
            TopologyNodeKind.Substation => "SubstationNodeTemplate",
            TopologyNodeKind.Busbar => "BusbarNodeTemplate",
            TopologyNodeKind.Load => "LoadNodeTemplate",
            _ => "GridNodeTemplate"
        };

        return element.TryFindResource(key) as DataTemplate;
    }
}
