using BlazorFeatures.Abstractions.Enums;
using BlazorFeatures.Base.Attributes;
using System.Runtime.CompilerServices;

[assembly: FeatureAssembly(RenderType.Client)]
[assembly: InternalsVisibleTo("BlazorFeatures.Base.Tests")]
[assembly: InternalsVisibleTo("BlazorFeatures.Base.Server")]
