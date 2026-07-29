using System;
using System.Linq;
using System.Reflection;
using QubiCostElectrical.Wpf.Features.PaymentCertificates;
using Xunit;

namespace QubiCostElectrical.Tests.Architecture;

/// <summary>
/// Verifica que las dependencias entre capas respetan la arquitectura limpia:
/// Domain → Application → Infrastructure → WPF (Composition).
/// Ninguna capa debe referenciar capas superiores.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(PaymentCertificate).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(PaymentCertificateService).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(SQLiteSchemaMigrator).Assembly;
    private static readonly Assembly WpfAssembly = typeof(PaymentCertificateWorkspaceViewModel).Assembly;

    [Fact]
    public void Domain_DoesNotReferenceApplication()
    {
        var refs = DomainAssembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r =>
            r.Name == ApplicationAssembly.GetName().Name);
    }

    [Fact]
    public void Domain_DoesNotReferenceInfrastructure()
    {
        var refs = DomainAssembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r =>
            r.Name == InfrastructureAssembly.GetName().Name);
    }

    [Fact]
    public void Domain_DoesNotReferenceWpf()
    {
        var refs = DomainAssembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r =>
            r.Name == WpfAssembly.GetName().Name);
    }

    [Fact]
    public void Application_ReferencesDomain()
    {
        var refs = ApplicationAssembly.GetReferencedAssemblies();
        Assert.Contains(refs, r =>
            r.Name == DomainAssembly.GetName().Name);
    }

    [Fact]
    public void Application_DoesNotReferenceInfrastructure()
    {
        var refs = ApplicationAssembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r =>
            r.Name == InfrastructureAssembly.GetName().Name);
    }

    [Fact]
    public void Application_DoesNotReferenceWpf()
    {
        var refs = ApplicationAssembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r =>
            r.Name == WpfAssembly.GetName().Name);
    }

    [Fact]
    public void Infrastructure_ReferencesDomain()
    {
        var refs = InfrastructureAssembly.GetReferencedAssemblies();
        Assert.Contains(refs, r =>
            r.Name == DomainAssembly.GetName().Name);
    }

    [Fact]
    public void Infrastructure_ReferencesApplication()
    {
        var refs = InfrastructureAssembly.GetReferencedAssemblies();
        Assert.Contains(refs, r =>
            r.Name == ApplicationAssembly.GetName().Name);
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceWpf()
    {
        var refs = InfrastructureAssembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r =>
            r.Name == WpfAssembly.GetName().Name);
    }

    [Fact]
    public void Wpf_ReferencesDomain()
    {
        var refs = WpfAssembly.GetReferencedAssemblies();
        Assert.Contains(refs, r =>
            r.Name == DomainAssembly.GetName().Name);
    }

    [Fact]
    public void Wpf_ReferencesApplication()
    {
        var refs = WpfAssembly.GetReferencedAssemblies();
        Assert.Contains(refs, r =>
            r.Name == ApplicationAssembly.GetName().Name);
    }

    [Fact]
    public void Wpf_ReferencesInfrastructure()
    {
        var refs = WpfAssembly.GetReferencedAssemblies();
        Assert.Contains(refs, r =>
            r.Name == InfrastructureAssembly.GetName().Name);
    }
}
