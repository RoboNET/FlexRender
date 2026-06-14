using FlexRender.Parsing.Nodes;
using YamlDotNet.RepresentationModel;

namespace FlexRender.Parsing;

/// <summary>
/// Converts a YamlDotNet representation-model tree into the format-neutral
/// <see cref="TemplateNode"/> model consumed by the shared Core parsing engine.
/// This is the only place YamlDotNet types cross into the neutral world.
/// </summary>
internal static class YamlNodeConverter
{
    /// <summary>Converts a YamlDotNet document root mapping to a neutral <see cref="TemplateMapping"/>.</summary>
    /// <param name="root">The YamlDotNet mapping node (document root).</param>
    /// <returns>The equivalent neutral mapping.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="root"/> is null.</exception>
    internal static TemplateMapping Convert(YamlMappingNode root)
    {
        System.ArgumentNullException.ThrowIfNull(root);
        return ConvertMapping(root);
    }

    private static TemplateNode ConvertNode(YamlNode node) => node switch
    {
        YamlMappingNode m => ConvertMapping(m),
        YamlSequenceNode s => ConvertSequence(s),
        YamlScalarNode sc => new TemplateScalar(sc.Value),
        // Aliases/anchors collapse to their resolved node in the representation model;
        // any unexpected node kind becomes an empty scalar to preserve total conversion.
        _ => new TemplateScalar(null)
    };

    private static TemplateMapping ConvertMapping(YamlMappingNode mapping)
    {
        var result = new TemplateMapping();
        foreach (var (keyNode, valueNode) in mapping.Children)
        {
            if (keyNode is YamlScalarNode { Value: { } key })
            {
                result.Add(key, ConvertNode(valueNode));
            }
        }
        return result;
    }

    private static TemplateSequence ConvertSequence(YamlSequenceNode sequence)
    {
        var result = new TemplateSequence(sequence.Children.Count);
        foreach (var child in sequence.Children)
        {
            result.Add(ConvertNode(child));
        }
        return result;
    }
}
