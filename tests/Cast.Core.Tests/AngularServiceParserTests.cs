using System;
using System.Diagnostics;
using System.Linq;
using Cast.Core.Diagnostics;
using Cast.Core.Models;
using Cast.Core.Services;

namespace Cast.Core.Tests;

public sealed class AngularServiceParserTests
{
    private static AngularService Parse(string source) => new AngularServiceParser().Parse(source, "test.ts");

    [Fact]
    public void Parse_InjectFieldClass_ExtractsServiceAndToken()
    {
        const string source = """
            import { HttpClient } from '@angular/common/http';
            import { Injectable, inject } from '@angular/core';
            import { API_BASE_URL } from '../api-base-url.token';

            @Injectable({ providedIn: 'root' })
            export class DashboardService {
              private readonly http = inject(HttpClient);
              private readonly baseUrl = inject(API_BASE_URL);
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal("DashboardService", service.Name);
        Assert.Equal(ConsumerKind.Service, service.Kind);
        Assert.False(service.IsFunctional);
        Assert.True(service.IsSingleton);
        Assert.Equal("root", service.ProvidedIn);

        Assert.Collection(
            service.Dependencies,
            d => { Assert.Equal("HttpClient", d.Name); Assert.Equal(DependencyKind.Service, d.Kind); Assert.False(d.Optional); },
            d => { Assert.Equal("API_BASE_URL", d.Name); Assert.Equal(DependencyKind.Token, d.Kind); });
    }

    [Fact]
    public void Parse_FunctionalInterceptor_IsFunctionalWithServiceDeps()
    {
        const string source = """
            import { HttpInterceptorFn } from '@angular/common/http';
            import { inject } from '@angular/core';
            import { Router } from '@angular/router';
            import { AuthStore } from './auth-store';

            export const authInterceptor: HttpInterceptorFn = (req, next) => {
              const auth = inject(AuthStore);
              const router = inject(Router);
              return next(req);
            };
            """;

        AngularService service = Parse(source);

        Assert.Equal("authInterceptor", service.Name);
        Assert.Equal(ConsumerKind.Interceptor, service.Kind);
        Assert.True(service.IsFunctional);
        Assert.False(service.IsSingleton);
        Assert.Equal(["AuthStore", "Router"], service.Dependencies.Select(d => d.Name));
        Assert.All(service.Dependencies, d => Assert.Equal(DependencyKind.Service, d.Kind));
    }

    [Fact]
    public void Parse_ConstructorInjection_DetectsInjectTokenAndOptional()
    {
        const string source = """
            import { Injectable, Optional, Inject } from '@angular/core';
            import { HttpClient } from '@angular/common/http';
            import { API_BASE_URL } from './tokens';
            import { LoggerService } from './logger.service';

            @Injectable()
            export class ReportService {
              constructor(
                private readonly http: HttpClient,
                @Inject(API_BASE_URL) private readonly baseUrl: string,
                @Optional() private readonly logger?: LoggerService,
              ) {}
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal("ReportService", service.Name);
        Assert.False(service.IsSingleton); // @Injectable() with no providedIn

        Assert.Collection(
            service.Dependencies,
            d => { Assert.Equal("HttpClient", d.Name); Assert.Equal(DependencyKind.Service, d.Kind); Assert.False(d.Optional); },
            d => { Assert.Equal("API_BASE_URL", d.Name); Assert.Equal(DependencyKind.Token, d.Kind); Assert.False(d.Optional); },
            d => { Assert.Equal("LoggerService", d.Name); Assert.Equal(DependencyKind.Service, d.Kind); Assert.True(d.Optional); });
    }

    [Fact]
    public void Parse_ZeroDependencyClass_HasNoDependenciesButKeepsSingleton()
    {
        const string source = """
            import { Injectable, signal } from '@angular/core';

            @Injectable({ providedIn: 'root' })
            export class AuthStore {
              private readonly _token = signal<string | null>(null);
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal("AuthStore", service.Name);
        Assert.Equal(ConsumerKind.Store, service.Kind);
        Assert.True(service.IsSingleton);
        Assert.Empty(service.Dependencies);
    }

    [Fact]
    public void Parse_CommentsAndStrings_DoNotProduceFalseDependencies()
    {
        const string source = """
            import { Injectable, inject } from '@angular/core';
            import { HttpClient } from '@angular/common/http';

            // inject(CommentService) should be ignored
            /* inject(BlockService) should be ignored too */
            @Injectable({ providedIn: 'root' })
            export class WidgetService {
              private readonly http = inject(HttpClient);
              private readonly note = 'call inject(StringService) here';
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal(["HttpClient"], service.Dependencies.Select(d => d.Name));
    }

    [Fact]
    public void Parse_OptionalInjectOption_MarksDependencyOptional()
    {
        const string source = """
            import { Injectable, inject } from '@angular/core';
            import { Telemetry } from './telemetry';

            @Injectable({ providedIn: 'root' })
            export class MetricsService {
              private readonly telemetry = inject(Telemetry, { optional: true });
            }
            """;

        AngularDependency dep = Assert.Single(Parse(source).Dependencies);
        Assert.Equal("Telemetry", dep.Name);
        Assert.True(dep.Optional);
    }

    [Fact]
    public void Parse_DuplicateInjectCalls_AreDeduplicated()
    {
        const string source = """
            import { Injectable, inject } from '@angular/core';
            import { HttpClient } from '@angular/common/http';

            @Injectable({ providedIn: 'root' })
            export class TwiceService {
              private readonly a = inject(HttpClient);
              other() { const b = inject(HttpClient); return b; }
            }
            """;

        Assert.Equal(["HttpClient"], Parse(source).Dependencies.Select(d => d.Name));
    }

    [Fact]
    public void Parse_LocalInjectionToken_LowercaseName_ClassifiedAsToken()
    {
        const string source = """
            import { Injectable, InjectionToken, inject } from '@angular/core';

            export const config = new InjectionToken<string>('config');

            @Injectable({ providedIn: 'root' })
            export class ConfiguredService {
              private readonly cfg = inject(config);
            }
            """;

        AngularDependency dep = Assert.Single(Parse(source).Dependencies);
        Assert.Equal("config", dep.Name);
        Assert.Equal(DependencyKind.Token, dep.Kind);
    }

    [Fact]
    public void Parse_FunctionalGuard_KindIsGuard()
    {
        const string source = """
            import { inject } from '@angular/core';
            import { CanActivateFn, Router } from '@angular/router';
            import { AuthStore } from 'api';

            export const authGuard: CanActivateFn = () => {
              const store = inject(AuthStore);
              return store.isAuthenticated();
            };
            """;

        AngularService service = Parse(source);
        Assert.Equal(ConsumerKind.Guard, service.Kind);
        Assert.True(service.IsFunctional);
    }

    [Fact]
    public void Parse_ProvidedInAny_IsNotApplicationSingleton()
    {
        const string source = """
            import { Injectable } from '@angular/core';

            @Injectable({ providedIn: 'any' })
            export class PerInjectorService {}
            """;

        AngularService service = Parse(source);
        Assert.False(service.IsSingleton);
        Assert.Equal("any", service.ProvidedIn);
    }

    [Fact]
    public void Parse_NoConsumer_Throws()
    {
        const string source = """
            export const TWO = 2;
            export function add(a: number, b: number): number { return a + b; }
            """;

        Assert.Throws<DiagramFormatException>(() => Parse(source));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptySource_Throws(string source)
    {
        Assert.Throws<DiagramFormatException>(() => Parse(source));
    }

    // ----- regression tests for the adversarial review findings ---------------------------

    [Fact]
    public void Parse_DtoClassBeforeService_PrefersDecoratedService()
    {
        const string source = """
            export class UserDto {
              id!: string;
              name!: string;
            }

            @Injectable({ providedIn: 'root' })
            export class UserService {
              private readonly http = inject(HttpClient);
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal("UserService", service.Name);
        Assert.True(service.IsSingleton);
        Assert.Equal(["HttpClient"], service.Dependencies.Select(d => d.Name));
    }

    [Fact]
    public void Parse_PrimitiveConstructorParams_AreNotDependencies()
    {
        const string source = """
            @Injectable()
            export class ConfigService {
              constructor(
                private readonly name: string,
                private readonly count: number,
                private readonly flag: boolean,
                private readonly http: HttpClient,
                @Inject(API_BASE_URL) private readonly baseUrl: string,
              ) {}
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal(["HttpClient", "API_BASE_URL"], service.Dependencies.Select(d => d.Name));
        Assert.DoesNotContain(service.Dependencies, d => d.Name is "string" or "number" or "boolean");
    }

    [Theory]
    [InlineData("inject<Map<string, number>>(CONFIG)", "CONFIG")]
    [InlineData("inject<Foo<Bar>>(MyService)", "MyService")]
    public void Parse_NestedGenericInject_CapturesDependency(string injectExpr, string expected)
    {
        string source = $$"""
            @Injectable({ providedIn: 'root' })
            export class GenericService {
              private readonly dep = {{injectExpr}};
            }
            """;

        AngularDependency dep = Assert.Single(Parse(source).Dependencies);
        Assert.Equal(expected, dep.Name);
    }

    [Fact]
    public void Parse_RegexLiteralContainingInject_DoesNotCreateFalseDependency()
    {
        const string source = """
            @Injectable({ providedIn: 'root' })
            export class RegexService {
              private readonly pattern = /inject(Fake)\{bar\}/;
              private readonly http = inject(HttpClient);
            }
            """;

        Assert.Equal(["HttpClient"], Parse(source).Dependencies.Select(d => d.Name));
    }

    [Fact]
    public void Parse_InjectStringLiteralToken_IsCaptured()
    {
        const string source = """
            @Injectable()
            export class StringTokenService {
              constructor(@Inject('app.config') private readonly cfg: any) {}
            }
            """;

        AngularDependency dep = Assert.Single(Parse(source).Dependencies);
        Assert.Equal("app.config", dep.Name);
        Assert.Equal(DependencyKind.Token, dep.Kind);
    }

    [Fact]
    public void Parse_ConstructorInjectOfPascalCaseToken_IsClassifiedAsToken()
    {
        const string source = """
            @Injectable()
            export class PascalTokenService {
              constructor(@Inject(MyConfig) private readonly cfg: MyConfig) {}
            }
            """;

        AngularDependency dep = Assert.Single(Parse(source).Dependencies);
        Assert.Equal("MyConfig", dep.Name);
        Assert.Equal(DependencyKind.Token, dep.Kind); // @Inject forces Token regardless of casing
    }

    [Fact]
    public void Parse_ProvidedInPlatform_IsSingleton()
    {
        const string source = """
            @Injectable({ providedIn: 'platform' })
            export class PlatformService {}
            """;

        AngularService service = Parse(source);
        Assert.True(service.IsSingleton);
        Assert.Equal("platform", service.ProvidedIn);
    }

    [Fact]
    public void Parse_InjectableOptionsWithNestedObjectBeforeProvidedIn_DetectsSingleton()
    {
        const string source = """
            @Injectable({ useFactory: () => null, providedIn: 'root' })
            export class NestedOptionsService {}
            """;

        AngularService service = Parse(source);
        Assert.True(service.IsSingleton);
        Assert.Equal("root", service.ProvidedIn);
    }

    [Fact]
    public void Parse_InjectInMethodBody_IsNotAConstructionDependency()
    {
        const string source = """
            @Injectable({ providedIn: 'root' })
            export class LazyService {
              private readonly http = inject(HttpClient);
              load() {
                const lazy = inject(LazyDep);
                return lazy;
              }
            }
            """;

        Assert.Equal(["HttpClient"], Parse(source).Dependencies.Select(d => d.Name));
    }

    [Fact]
    public void Parse_InjectFieldAndConstructorSameDependency_AppearsOnce()
    {
        const string source = """
            @Injectable({ providedIn: 'root' })
            export class MixedService {
              private readonly http = inject(HttpClient);
              constructor(private readonly http2: HttpClient) {}
            }
            """;

        Assert.Equal(["HttpClient"], Parse(source).Dependencies.Select(d => d.Name));
    }

    [Fact]
    public void Parse_ClassAndArrowConstInSameFile_PrefersClass()
    {
        const string source = """
            @Injectable({ providedIn: 'root' })
            export class FooService {
              private readonly http = inject(HttpClient);
            }

            export const fooInterceptor: HttpInterceptorFn = (req, next) => {
              const auth = inject(AuthStore);
              return next(req);
            };
            """;

        AngularService service = Parse(source);
        Assert.Equal("FooService", service.Name);
        Assert.False(service.IsFunctional);
    }

    [Fact]
    public void Parse_PathologicalTypedConstWithoutInitializer_DoesNotHang()
    {
        // A typed `export const` whose huge body contains no `=` previously triggered catastrophic
        // regex backtracking. It must now fail fast (no consumer) rather than hang.
        string source = "export const X: " + new string('a', 50_000);

        var stopwatch = Stopwatch.StartNew();
        Assert.Throws<DiagramFormatException>(() => Parse(source));
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 5_000, $"parsing took {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Parse_OverlargeSource_Throws()
    {
        Assert.Throws<DiagramFormatException>(() => Parse(new string('a', 2_000_001)));
    }

    // ----- any inject()-using construct (functions, components, …) -------------------------

    [Fact]
    public void Parse_ExportedFunctionDeclaration_WithInject_IsFunctionalConsumer()
    {
        const string source = """
            import { inject } from '@angular/core';
            import { HttpClient } from '@angular/common/http';

            export function loadRemoteConfig() {
              const http = inject(HttpClient);
              return http.get('/config');
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal("loadRemoteConfig", service.Name);
        Assert.True(service.IsFunctional);
        Assert.Equal(["HttpClient"], service.Dependencies.Select(d => d.Name));
    }

    [Fact]
    public void Parse_AsyncFunctionDeclaration_WithTokenInject_IsCaptured()
    {
        const string source = """
            import { inject } from '@angular/core';
            import { APP_CONFIG } from './tokens';

            export async function initialiseApp() {
              const config = inject(APP_CONFIG);
              return config.ready;
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal("initialiseApp", service.Name);
        Assert.True(service.IsFunctional);
        AngularDependency dep = Assert.Single(service.Dependencies);
        Assert.Equal("APP_CONFIG", dep.Name);
        Assert.Equal(DependencyKind.Token, dep.Kind);
    }

    [Fact]
    public void Parse_FunctionDeclarationGuard_KindIsGuard()
    {
        const string source = """
            import { inject } from '@angular/core';
            import { AuthStore } from './auth-store';

            export function authGuard() {
              const store = inject(AuthStore);
              return store.isAuthenticated();
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal(ConsumerKind.Guard, service.Kind);
        Assert.True(service.IsFunctional);
        Assert.Equal(["AuthStore"], service.Dependencies.Select(d => d.Name));
    }

    [Fact]
    public void Parse_FunctionDeclarationWithoutInject_IsNotConsumer()
    {
        const string source = """
            export function add(a: number, b: number): number {
              return a + b;
            }
            """;

        Assert.Throws<DiagramFormatException>(() => Parse(source));
    }

    [Fact]
    public void Parse_OnlyInjectingFunctionAmongPlainFunctions_PicksTheInjectingOne()
    {
        const string source = """
            import { inject } from '@angular/core';
            import { HttpClient } from '@angular/common/http';

            export function helperA(x: number): number { return x + 1; }

            export function buildClient() {
              const http = inject(HttpClient);
              return http;
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal("buildClient", service.Name);
        Assert.Equal(["HttpClient"], service.Dependencies.Select(d => d.Name));
    }

    [Fact]
    public void Parse_ComponentWithInject_IsComponentKind()
    {
        const string source = """
            import { Component, inject } from '@angular/core';
            import { UserService } from './user.service';

            @Component({ selector: 'app-profile', template: '<p>{{ name }}</p>' })
            export class ProfilePage {
              private readonly users = inject(UserService);
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal("ProfilePage", service.Name);
        Assert.Equal(ConsumerKind.Component, service.Kind);
        Assert.False(service.IsFunctional);
        Assert.Equal(["UserService"], service.Dependencies.Select(d => d.Name));
    }

    [Fact]
    public void Parse_DirectiveWithInject_IsDirectiveKind()
    {
        const string source = """
            import { Directive, inject } from '@angular/core';
            import { ElementRef } from '@angular/core';

            @Directive({ selector: '[appHighlight]' })
            export class HighlightThing {
              private readonly el = inject(ElementRef);
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal(ConsumerKind.Directive, service.Kind);
        Assert.Equal(["ElementRef"], service.Dependencies.Select(d => d.Name));
    }

    [Fact]
    public void Parse_DtoClassPlusInjectingFunction_PrefersTheFunction()
    {
        // A plain (non-DI) class must not shadow a function that actually participates in DI.
        const string source = """
            import { inject } from '@angular/core';
            import { HttpClient } from '@angular/common/http';

            export class UserDto {
              id!: string;
              name!: string;
            }

            export function provideUserClient() {
              const http = inject(HttpClient);
              return http;
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal("provideUserClient", service.Name);
        Assert.True(service.IsFunctional);
        Assert.Equal(["HttpClient"], service.Dependencies.Select(d => d.Name));
    }

    [Fact]
    public void Parse_FunctionDeclarationWithObjectReturnType_StillFindsInject()
    {
        // An inline object-literal return type must not be mistaken for the function body.
        const string source = """
            import { inject } from '@angular/core';
            import { HttpClient } from '@angular/common/http';

            export function buildConfig(): { name: string } {
              const http = inject(HttpClient);
              return { name: 'x' };
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal("buildConfig", service.Name);
        Assert.True(service.IsFunctional);
        Assert.Equal(["HttpClient"], service.Dependencies.Select(d => d.Name));
    }

    [Fact]
    public void Parse_UndecoratedSuffixedClassPlusInjectingFunction_PrefersTheFunction()
    {
        // A class merely named like a service but missing @Injectable does not participate in
        // Angular DI, so a real injecting function in the same file must win.
        const string source = """
            import { HttpInterceptorFn } from '@angular/common/http';
            import { inject } from '@angular/core';
            import { AuthStore } from './auth-store';

            export class LoggingService {
              log(message: string): void {}
            }

            export const authInterceptor: HttpInterceptorFn = (req, next) => {
              const auth = inject(AuthStore);
              return next(req);
            };
            """;

        AngularService service = Parse(source);

        Assert.Equal("authInterceptor", service.Name);
        Assert.Equal(ConsumerKind.Interceptor, service.Kind);
        Assert.True(service.IsFunctional);
        Assert.Equal(["AuthStore"], service.Dependencies.Select(d => d.Name));
    }

    [Fact]
    public void Parse_LoneUndecoratedClassWithInject_StillParsedViaFallback()
    {
        // With no functional consumer present, an undecorated class is still the subject.
        const string source = """
            import { inject } from '@angular/core';
            import { HttpClient } from '@angular/common/http';

            export class PlainService {
              private readonly http = inject(HttpClient);
            }
            """;

        AngularService service = Parse(source);

        Assert.Equal("PlainService", service.Name);
        Assert.False(service.IsFunctional);
        Assert.Equal(["HttpClient"], service.Dependencies.Select(d => d.Name));
    }
}
