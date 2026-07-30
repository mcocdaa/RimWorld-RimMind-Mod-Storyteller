# RimMind Storyteller contract tests

The project compiles only the active behavior contracts and explicitly linked
production seams. Legacy files remain on disk but are excluded.

## Active contract manifest

| Contract | Stable boundaries | Discovered facts |
|---|---|---:|
| `Contracts/StorytellerIncidentPolicyContracts.cs` | response parsing, incident policy, pawn lookup, notification policy | 1 |
| `Contracts/StorytellerRequestContextContracts.cs` | context scoping/composition, memory bridge, tension, request state | 1 |
| `Contracts/StorytellerSaveErrorContracts.cs` | persistence codec, malformed-load normalization, request failure isolation | 1 |

Current discovery count: **3 Facts**, **0 Theories** (budget: <= 40).
Each Fact uses `ContractCaseRunner` to report its named scenarios independently.

## Persistence coverage level

`StorytellerSaveErrorContracts` executes the production persistence codec with a
behavior-capable Scribe recorder. It verifies stable keys/defaults/modes, null
collection recovery, request failure isolation and single-consumption success
state without compiling RimWorld adapters.

## Active project entry

The compact pure-logic cutover needs:

```xml
<PropertyGroup>
  <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
</PropertyGroup>
<ItemGroup>
  <Compile Include="VerseStubs.cs" />
  <Compile Include="Contracts\**\*.cs" />
  <Compile Include="..\..\RimMind-Core\TestSupport\ContractCaseRunner.cs"
           Link="Support\ContractCaseRunner.cs" />
  <Compile Include="..\Source\Memory\TensionMath.cs" LinkBase="Memory" />
  <Compile Include="..\Source\Memory\IncidentResponse.cs" LinkBase="Memory" />
  <Compile Include="..\Source\Memory\StorytellerPersistenceCodec.cs" LinkBase="Memory" />
  <Compile Include="..\Source\Storyteller\StorytellerResponseParserPure.cs" LinkBase="Storyteller" />
  <Compile Include="..\Source\Storyteller\IncidentSelectionPolicy.cs" LinkBase="Storyteller" />
  <Compile Include="..\Source\Storyteller\StorytellerRequestState.cs" LinkBase="Storyteller" />
  <Compile Include="..\Source\Extensions\PawnLookupCore.cs" LinkBase="Extensions" />
  <Compile Include="..\Source\Extensions\StorytellerContextPolicy.cs" LinkBase="Extensions" />
  <Compile Include="..\Source\Extensions\StorytellerMemoryBridge.cs" LinkBase="Extensions" />
</ItemGroup>
```

Legacy compile categories superseded by these contracts are:

- incident response/parser, incident selector, pawn lookup, and notification tests;
- request envelope, context provider, prompt, memory bridge, tension, and decay tests;
- architecture-direction tests covered by the request/context contract.

## Retired legacy tests

Files outside `Contracts/` are retained on disk but excluded from compilation.
Their behavior mapping is recorded in the root contract mapping document.
Deletion requires explicit owner approval for each exact file path; directories are never deleted.
