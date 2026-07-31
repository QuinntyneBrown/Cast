using System;
using System.Diagnostics;
using System.Linq;
using Cast.Core.Diagnostics;
using Cast.Core.Models;
using Cast.Core.Services;

namespace Cast.Core.Tests;

public sealed class TypeScriptCallGraphParserTests
{
    private static CallGraph Parse(string source, string fileName = "test.ts") =>
        new TypeScriptCallGraphParser().Parse(source, fileName);

    [Fact]
    public void Parse_ClassMethods_ResolvesEveryReceiverKind()
    {
        const string source = """
            import { inject } from '@angular/core';
            import { HttpClient } from '@angular/common/http';

            @Injectable({ providedIn: 'root' })
            export class OrderService {
              private readonly http = inject(HttpClient);
              constructor(private readonly repo: OrderRepository) {}

              async place(id: string, payload: unknown): Promise<void> {
                this.validate(id);
                const saved = await this.repo.save(payload);
                this.http.post('/orders', saved);
                console.log('placed');
                const audit = new AuditEntry(id);
                audit.commit();
              }

              private validate(id: string): void {}
            }
            """;

        CallGraph graph = Parse(source);

        Assert.Equal("OrderService", graph.SubjectName);
        Assert.Equal(CallGraphSubjectKind.Class, graph.Kind);

        CallGraphMethod place = graph.Methods.Single(m => m.Name == "place");
        Assert.Equal("place(id, payload)", place.Signature);
        Assert.Equal(MethodVisibility.Public, place.Visibility);
        Assert.True(place.IsAsync);
        Assert.False(place.IsStatic);

        // console.log is dropped as noise; everything else resolves in source order.
        Assert.Collection(
            place.Calls,
            c => { Assert.Equal(CallKind.Self, c.Kind); Assert.Equal("validate", c.Method); },
            c => { Assert.Equal(CallKind.Collaborator, c.Kind); Assert.Equal("OrderRepository", c.ReceiverKey); Assert.Equal("save", c.Method); },
            c => { Assert.Equal(CallKind.Collaborator, c.Kind); Assert.Equal("HttpClient", c.ReceiverKey); Assert.Equal("post", c.Method); },
            c => { Assert.Equal(CallKind.Construction, c.Kind); Assert.Equal("AuditEntry", c.ReceiverKey); },
            c => { Assert.Equal(CallKind.Collaborator, c.Kind); Assert.Equal("AuditEntry", c.ReceiverKey); Assert.Equal("commit", c.Method); });
    }

    [Fact]
    public void Parse_PrivateMethod_IsReturnedWithPrivateVisibility()
    {
        const string source = """
            export class Service {
              run(): void { this.help(); }
              private help(): void {}
            }
            """;

        CallGraph graph = Parse(source);

        Assert.Equal(["run", "help"], graph.Methods.Select(m => m.Name));
        Assert.Equal(MethodVisibility.Public, graph.Methods[0].Visibility);
        Assert.Equal(MethodVisibility.Private, graph.Methods[1].Visibility);
    }

    [Theory]
    [InlineData("protected guard(): void {}", "guard", MethodVisibility.Protected)]
    [InlineData("private secret(): void {}", "secret", MethodVisibility.Private)]
    [InlineData("#hard(): void {}", "#hard", MethodVisibility.Private)]
    [InlineData("open(): void {}", "open", MethodVisibility.Public)]
    public void Parse_Visibility_IsDetectedFromModifiers(string member, string name, MethodVisibility expected)
    {
        string source = $$"""
            export class C {
              {{member}}
            }
            """;

        CallGraphMethod method = Assert.Single(Parse(source).Methods);
        Assert.Equal(name, method.Name);
        Assert.Equal(expected, method.Visibility);
    }

    [Fact]
    public void Parse_StaticAndAsyncModifiers_AreCaptured()
    {
        const string source = """
            export class C {
              static create(): void {}
              async load(): Promise<void> {}
            }
            """;

        CallGraph graph = Parse(source);
        Assert.True(graph.Methods.Single(m => m.Name == "create").IsStatic);
        Assert.True(graph.Methods.Single(m => m.Name == "load").IsAsync);
    }

    [Fact]
    public void Parse_Signature_HandlesModifiersRestAndDestructuring()
    {
        const string source = """
            export class C {
              m(a: string, b = 5, ...rest: number[]): void {}
              n({ x, y }: Point, [a, b]: Pair): void {}
            }
            """;

        CallGraph graph = Parse(source);
        Assert.Equal("m(a, b, ...rest)", graph.Methods.Single(x => x.Name == "m").Signature);
        Assert.Equal("n(…, …)", graph.Methods.Single(x => x.Name == "n").Signature);
    }

    [Fact]
    public void Parse_ConstructorParameterProperty_ResolvesLaterCalls()
    {
        const string source = """
            export class C {
              constructor(private readonly repo: OrderRepository) {}
              run(): void { this.repo.save(); }
            }
            """;

        MethodCall call = Assert.Single(Parse(source).Methods.Single(m => m.Name == "run").Calls);
        Assert.Equal(CallKind.Collaborator, call.Kind);
        Assert.Equal("OrderRepository", call.ReceiverKey);
        Assert.Equal("save", call.Method);
    }

    [Fact]
    public void Parse_Constructor_IsNotDiagrammedAsAMethod()
    {
        const string source = """
            export class C {
              constructor(private readonly repo: Repo) {}
              run(): void {}
            }
            """;

        Assert.Equal(["run"], Parse(source).Methods.Select(m => m.Name));
    }

    [Fact]
    public void Parse_ClassWithOnlyConstructor_Throws()
    {
        const string source = """
            export class C {
              constructor() {}
            }
            """;

        Assert.Throws<DiagramFormatException>(() => Parse(source));
    }

    [Fact]
    public void Parse_SuperCall_IsASelfCall()
    {
        const string source = """
            export class C extends Base {
              init(): void { super.init(); }
            }
            """;

        MethodCall call = Assert.Single(Parse(source).Methods.Single(m => m.Name == "init").Calls);
        Assert.Equal(CallKind.Self, call.Kind);
        Assert.Equal("init", call.Method);
    }

    [Fact]
    public void Parse_CallOnPrimitiveTypedValue_IsDroppedAsNoise()
    {
        const string source = """
            export class C {
              m(id: string): void {
                id.toUpperCase();
                this.real();
              }
              real(): void {}
            }
            """;

        MethodCall call = Assert.Single(Parse(source).Methods.Single(m => m.Name == "m").Calls);
        Assert.Equal(CallKind.Self, call.Kind);
        Assert.Equal("real", call.Method);
    }

    [Fact]
    public void Parse_StaticClassReceiver_IsACollaborator()
    {
        const string source = """
            export class C {
              m(): void { Math.max(1, 2); }
            }
            """;

        MethodCall call = Assert.Single(Parse(source).Methods.Single(m => m.Name == "m").Calls);
        Assert.Equal(CallKind.Collaborator, call.Kind);
        Assert.Equal("Math", call.ReceiverKey);
        Assert.Equal("max", call.Method);
    }

    [Fact]
    public void Parse_OptionalChainingAndNonNullAssertion_AreResolved()
    {
        const string source = """
            export class C {
              private readonly repo = inject(OrderRepository);
              run(): void {
                this.repo?.save();
                this!.helper();
                this.repo!.flush();
              }
              helper(): void {}
            }
            """;

        CallGraphMethod run = Parse(source).Methods.Single(m => m.Name == "run");
        Assert.Contains(run.Calls, c => c.Kind == CallKind.Collaborator && c.ReceiverKey == "OrderRepository" && c.Method == "save");
        Assert.Contains(run.Calls, c => c.Kind == CallKind.Collaborator && c.ReceiverKey == "OrderRepository" && c.Method == "flush");
        Assert.Contains(run.Calls, c => c.Kind == CallKind.Self && c.Method == "helper");
    }

    [Fact]
    public void Parse_DuplicateCalls_AreDeduplicated()
    {
        const string source = """
            export class C {
              private readonly http = inject(HttpClient);
              m(): void {
                this.http.get();
                this.http.get();
              }
            }
            """;

        MethodCall call = Assert.Single(Parse(source).Methods.Single(m => m.Name == "m").Calls);
        Assert.Equal("HttpClient", call.ReceiverKey);
        Assert.Equal("get", call.Method);
    }

    [Fact]
    public void Parse_BareCallToUnknownGlobal_IsIgnored()
    {
        const string source = """
            export class C {
              m(): void {
                parseInt('5');
                this.real();
              }
              real(): void {}
            }
            """;

        // parseInt is not a sibling method, so it is treated as a library/global call and dropped.
        MethodCall call = Assert.Single(Parse(source).Methods.Single(m => m.Name == "m").Calls);
        Assert.Equal("real", call.Method);
    }

    [Fact]
    public void Parse_CommentsAndStrings_DoNotProduceFalseCalls()
    {
        const string source = """
            export class C {
              m(): void {
                // this.fake(x)
                /* this.blockFake(y) */
                const s = 'this.stringFake(z)';
                this.real();
              }
              real(): void {}
            }
            """;

        MethodCall call = Assert.Single(Parse(source).Methods.Single(m => m.Name == "m").Calls);
        Assert.Equal("real", call.Method);
    }

    [Fact]
    public void Parse_RegexLiteralLookingLikeCall_IsIgnored()
    {
        const string source = """
            export class C {
              m(): void {
                const re = /this\.fake\(x\)/;
                this.real();
              }
              real(): void {}
            }
            """;

        MethodCall call = Assert.Single(Parse(source).Methods.Single(m => m.Name == "m").Calls);
        Assert.Equal("real", call.Method);
    }

    [Fact]
    public void Parse_OverloadSignatures_AreSkipped_OnlyTheImplementationIsKept()
    {
        const string source = """
            export class C {
              run(a: string): void;
              run(a: number): void;
              run(a: any): void { this.real(); }
              real(): void {}
            }
            """;

        CallGraphMethod run = Assert.Single(Parse(source).Methods, m => m.Name == "run");
        MethodCall call = Assert.Single(run.Calls);
        Assert.Equal("real", call.Method);
    }

    [Fact]
    public void Parse_DecoratedMember_IsNotMistakenForAMethod()
    {
        const string source = """
            export class C {
              @Input() title!: string;
              @HostListener('click') onClick(): void { this.real(); }
              real(): void {}
            }
            """;

        CallGraph graph = Parse(source);
        Assert.DoesNotContain(graph.Methods, m => m.Name is "Input" or "HostListener");
        Assert.Contains(graph.Methods, m => m.Name == "onClick");
    }

    [Fact]
    public void Parse_PrefersExportedClassOverEarlierHelperClass()
    {
        const string source = """
            class InternalHelper {
              help(): void {}
            }

            export class PublicService {
              run(): void {}
            }
            """;

        Assert.Equal("PublicService", Parse(source).SubjectName);
    }

    // ----- module (no class) ---------------------------------------------------------------

    [Fact]
    public void Parse_ModuleFunctions_AreDiagrammedWithModuleSubject()
    {
        const string source = """
            import { Money } from './money';

            export function computeTotal(items: LineItem[]): Money {
              const subtotal = sumLines(items);
              return applyTax(subtotal);
            }

            export const applyTax = (amount: Money): Money => {
              return amount.multiply(1.2);
            };

            function sumLines(items: LineItem[]): Money {
              return Money.zero();
            }
            """;

        CallGraph graph = Parse(source, "pricing.utils.ts");

        Assert.Equal("pricing", graph.SubjectName);
        Assert.Equal(CallGraphSubjectKind.Module, graph.Kind);
        Assert.Equal(["computeTotal", "applyTax"], graph.Methods.Select(m => m.Name));

        // A call to a non-exported sibling function is still a self call on the module.
        CallGraphMethod compute = graph.Methods.Single(m => m.Name == "computeTotal");
        Assert.Contains(compute.Calls, c => c.Kind == CallKind.Self && c.Method == "sumLines");
        Assert.Contains(compute.Calls, c => c.Kind == CallKind.Self && c.Method == "applyTax");

        CallGraphMethod tax = graph.Methods.Single(m => m.Name == "applyTax");
        Assert.Contains(tax.Calls, c => c.ReceiverKey == "Money" && c.Method == "multiply");
    }

    [Fact]
    public void Parse_AsyncArrowConstExport_IsAsyncAndResolvesCalls()
    {
        const string source = """
            export const loadUser = async (id: string) => {
              const repo = inject(UserRepository);
              return repo.find(id);
            };
            """;

        CallGraphMethod fn = Assert.Single(Parse(source, "user.api.ts").Methods);
        Assert.Equal("loadUser", fn.Name);
        Assert.True(fn.IsAsync);
        Assert.Contains(fn.Calls, c => c.ReceiverKey == "UserRepository" && c.Method == "find");
    }

    [Fact]
    public void Parse_ExpressionBodiedArrowsWithoutSemicolons_DoNotBleedTogether()
    {
        // Prettier's no-semicolon style: an expression body must stop at the next top-level
        // declaration, not run to end-of-file and absorb the following function's calls.
        const string source = "export const first = () => alpha.one()\nexport const second = () => beta.two()\n";

        CallGraph graph = Parse(source, "m.ts");
        CallGraphMethod first = graph.Methods.Single(m => m.Name == "first");
        CallGraphMethod second = graph.Methods.Single(m => m.Name == "second");

        Assert.Contains(first.Calls, c => c.ReceiverKey == "alpha" && c.Method == "one");
        Assert.DoesNotContain(first.Calls, c => c.Method == "two");
        Assert.Contains(second.Calls, c => c.ReceiverKey == "beta" && c.Method == "two");
    }

    [Fact]
    public void Parse_MultiLineExpressionBodiedArrow_KeepsTheWholeBody()
    {
        // A continued expression (lines that do not start a new statement) stays part of the body,
        // and a nested '=>' inside it must not corrupt bracket tracking and truncate the body early.
        const string source = """
            export const build = () =>
              first.run(x => x) ||
              second.run();
            """;

        CallGraphMethod build = Assert.Single(Parse(source, "m.ts").Methods);
        Assert.Contains(build.Calls, c => c.ReceiverKey == "first" && c.Method == "run");
        Assert.Contains(build.Calls, c => c.ReceiverKey == "second" && c.Method == "run");
    }

    [Fact]
    public void Parse_NoClassOrFunction_Throws()
    {
        const string source = """
            export const TWO = 2;
            export interface Foo { id: string; }
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

    [Fact]
    public void Parse_OverlargeSource_Throws()
    {
        Assert.Throws<DiagramFormatException>(() => Parse(new string('a', 2_000_001)));
    }

    [Fact]
    public void Parse_PathologicalInput_DoesNotHang()
    {
        string source = "export class C { m(): void { " + new string('(', 20_000) + " } }";

        var stopwatch = Stopwatch.StartNew();
        try
        {
            Parse(source);
        }
        catch (DiagramFormatException)
        {
            // a parse failure is fine; hanging is not
        }

        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds < 5_000, $"parsing took {stopwatch.ElapsedMilliseconds} ms");
    }
}
