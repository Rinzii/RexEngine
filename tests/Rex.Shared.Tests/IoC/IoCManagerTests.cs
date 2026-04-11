using Rex.Shared.IoC;

namespace Rex.Shared.Tests.IoC;

public sealed class IoCManagerTests
{
    public IoCManagerTests()
    {
        IoCManager.Clear();
    }

    [Fact]
    public void Resolve_returns_singleton_instance()
    {
        IoCManager.RegisterInstance<ITestLogger>(new TestLogger());
        IoCManager.Register<ITestDependency, TestDependency>();

        ITestDependency first = IoCManager.Resolve<ITestDependency>();
        ITestDependency second = IoCManager.Resolve<ITestDependency>();

        Assert.Same(first, second);
    }

    [Fact]
    public void Inject_dependencies_fills_fields_and_calls_post_inject()
    {
        TestLogger logger = new();
        IoCManager.RegisterInstance<ITestLogger>(logger);
        IoCManager.Register<ITestDependency, TestDependency>();

        Consumer consumer = new();
        IoCManager.InjectDependencies(consumer);

        Assert.NotNull(consumer.Dependency);
        Assert.True(consumer.PostInjected);
        Assert.Equal("post", logger.Messages.Single());
    }

    [Fact]
    public void Registered_singletons_support_circular_field_injection()
    {
        IoCManager.Register<IA, A>();
        IoCManager.Register<IB, B>();

        IA a = IoCManager.Resolve<IA>();
        IB b = IoCManager.Resolve<IB>();

        Assert.Same(b, ((A)a).B);
        Assert.Same(a, ((B)b).A);
    }

    private interface ITestLogger
    {
        List<string> Messages { get; }
    }

    private sealed class TestLogger : ITestLogger
    {
        public List<string> Messages { get; } = [];
    }

    private interface ITestDependency
    {
    }

    private sealed class TestDependency : ITestDependency, IPostInjectInit
    {
        [Dependency]
        private readonly ITestLogger _logger = default!;

        public void PostInject()
        {
            _logger.Messages.Add("post");
        }
    }

    private sealed class Consumer : IPostInjectInit
    {
        [field: Dependency]
        public ITestDependency Dependency { get; } = default!;

        public bool PostInjected { get; private set; }

        public void PostInject()
        {
            PostInjected = true;
        }
    }

    private interface IA
    {
    }

    private interface IB
    {
    }

    private sealed class A : IA
    {
        [field: Dependency]
        public IB B { get; } = default!;
    }

    private sealed class B : IB
    {
        [field: Dependency]
        public IA A { get; } = default!;
    }
}
