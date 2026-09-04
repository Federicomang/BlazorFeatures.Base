using BlazorFeatures.Abstractions;
using BlazorFeatures.Abstractions.Enums;
using BlazorFeatures.Base.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorFeatures.Base.Tests;

public class FeatureTypeResolverTests
{
    [Fact]
    public void Resolve_prefers_exact_feature_over_generic_feature()
    {
        var resolver = new FeatureTypeResolver([
            Descriptor(typeof(StringFeature), FeatureContract(typeof(StringFeature))),
            Descriptor(typeof(GenericFeature<>), FeatureContract(typeof(GenericFeature<>)))
        ]);

        var resolution = resolver.Resolve(typeof(GenericRequest<string>), typeof(GenericResponse<string>));

        Assert.Equal(typeof(StringFeature), resolution.ImplementationType);
        Assert.False(resolution.WasClosedFromGeneric);
    }

    [Fact]
    public void Resolve_closes_compatible_generic_feature()
    {
        var descriptor = Descriptor(
            typeof(GenericFeature<>),
            FeatureContract(typeof(GenericFeature<>)));
        var resolver = new FeatureTypeResolver([descriptor]);

        var resolution = resolver.Resolve(typeof(GenericRequest<int>), typeof(GenericResponse<int>));

        Assert.Equal(typeof(GenericFeature<int>), resolution.ImplementationType);
        Assert.Same(descriptor, resolution.Descriptor);
        Assert.True(resolution.WasClosedFromGeneric);
    }

    [Fact]
    public void Constructor_rejects_duplicate_active_closed_features()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new FeatureTypeResolver([
            Descriptor(typeof(FirstFeature), FeatureContract(typeof(FirstFeature))),
            Descriptor(typeof(SecondFeature), FeatureContract(typeof(SecondFeature)))
        ]));

        Assert.Contains(typeof(FirstFeature).FullName!, exception.Message);
        Assert.Contains(typeof(SecondFeature).FullName!, exception.Message);
        Assert.Contains(typeof(TestRequest).FullName!, exception.Message);
    }

    [Fact]
    public void Constructor_ignores_duplicate_feature_from_inactive_render_target()
    {
        var active = Descriptor(typeof(FirstFeature), FeatureContract(typeof(FirstFeature)));
        var inactive = Descriptor(
            typeof(SecondFeature),
            FeatureContract(typeof(SecondFeature)),
            isActive: false,
            renderType: RenderType.Client);
        var resolver = new FeatureTypeResolver([active, inactive]);

        var resolution = resolver.Resolve(typeof(TestRequest), typeof(TestResponse));

        Assert.Equal(typeof(FirstFeature), resolution.ImplementationType);
    }

    [Fact]
    public void Resolve_reports_all_ambiguous_generic_features()
    {
        var resolver = new FeatureTypeResolver([
            Descriptor(typeof(GenericFeature<>), FeatureContract(typeof(GenericFeature<>))),
            Descriptor(typeof(OtherGenericFeature<>), FeatureContract(typeof(OtherGenericFeature<>)))
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve(typeof(GenericRequest<Guid>), typeof(GenericResponse<Guid>)));

        Assert.Contains(typeof(GenericFeature<Guid>).FullName!, exception.Message);
        Assert.Contains(typeof(OtherGenericFeature<Guid>).FullName!, exception.Message);
    }

    [Fact]
    public void Constructor_rejects_generic_arguments_that_cannot_be_inferred()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new FeatureTypeResolver([
            Descriptor(typeof(UninferableFeature<>), FeatureContract(typeof(UninferableFeature<>)))
        ]));

        Assert.Contains("cannot be inferred", exception.Message);
        Assert.Contains("TUnused", exception.Message);
    }

    [Fact]
    public void AddFeatures_rejects_explicit_assembly_without_feature_attribute()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddFeatures(features =>
            features.AddAssemblyContaining<FeatureTypeResolverTests>()));

        Assert.Contains(nameof(BlazorFeatures.Base.Attributes.FeatureAssemblyAttribute), exception.Message);
        Assert.Contains(typeof(FeatureTypeResolverTests).Assembly.GetName().Name!, exception.Message);
    }

    private static FeatureDescriptor Descriptor(
        Type implementation,
        Type contract,
        bool isActive = true,
        RenderType renderType = RenderType.Server)
    {
        var arguments = contract.GetGenericArguments();
        return new FeatureDescriptor(
            contract,
            arguments[0],
            arguments[1],
            implementation,
            renderType,
            ServiceLifetime.Scoped,
            isActive);
    }

    private static Type FeatureContract(Type implementation) => implementation.GetInterfaces()
        .Single(candidate => candidate.IsGenericType
            && candidate.GetGenericTypeDefinition() == typeof(IBaseFeature<,>));

    public sealed class TestRequest : IBaseFeatureRequest<TestResponse>;

    public sealed class TestResponse;

    public sealed class GenericRequest<T> : IBaseFeatureRequest<GenericResponse<T>>;

    public sealed class GenericResponse<T>;

    public sealed class FirstFeature : TestFeature;

    public sealed class SecondFeature : TestFeature;

    public abstract class TestFeature : IBaseFeature<TestRequest, TestResponse>
    {
        public Task<FeatureResponse<TestResponse>> HandleClient(
            TestRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FeatureResponse<TestResponse>.AsSuccess(new()));

        public Task<FeatureResponse<TestResponse>> HandleServer(
            TestRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FeatureResponse<TestResponse>.AsSuccess(new()));
    }

    public sealed class StringFeature : IBaseFeature<GenericRequest<string>, GenericResponse<string>>
    {
        public Task<FeatureResponse<GenericResponse<string>>> HandleClient(
            GenericRequest<string> request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FeatureResponse<GenericResponse<string>>.AsSuccess(new()));

        public Task<FeatureResponse<GenericResponse<string>>> HandleServer(
            GenericRequest<string> request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FeatureResponse<GenericResponse<string>>.AsSuccess(new()));
    }

    public sealed class GenericFeature<T> : GenericFeatureBase<T>;

    public sealed class OtherGenericFeature<T> : GenericFeatureBase<T>;

    public abstract class GenericFeatureBase<T> : IBaseFeature<GenericRequest<T>, GenericResponse<T>>
    {
        public Task<FeatureResponse<GenericResponse<T>>> HandleClient(
            GenericRequest<T> request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FeatureResponse<GenericResponse<T>>.AsSuccess(new()));

        public Task<FeatureResponse<GenericResponse<T>>> HandleServer(
            GenericRequest<T> request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FeatureResponse<GenericResponse<T>>.AsSuccess(new()));
    }

    public sealed class UninferableFeature<TUnused> : TestFeature;
}
