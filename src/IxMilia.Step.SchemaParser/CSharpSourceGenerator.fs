namespace IxMilia.Step.SchemaParser

open System

module CSharpSourceGenerator =
    let private indentation = "    "
    let toLines (text: string) = text.Split('\n')
    let indentLine (line: string) = if line.Length > 0 then indentation + line else line
    let indentLines lines = lines |> Seq.map indentLine
    let joinWith (sep: string) (parts: string seq) = String.Join(sep, parts)
    let joinLines = joinWith "\n"
    let getIdentifierName (nameFromSchema: string) =
        nameFromSchema.Split('_')
        |> Array.map (fun p -> p.Substring(0, 1).ToUpperInvariant() + p.Substring(1))
        |> Array.fold (+) ""
    let getParameterName (nameFromSchema: string) =
        let identifierName = getIdentifierName nameFromSchema
        identifierName.Substring(0, 1).ToLowerInvariant() + identifierName.Substring(1)
    let getFieldName (nameFromSchema: string) = "_" + getParameterName nameFromSchema
    let getIdentifierNameWithPrefix (nameFromSchema: string) (typeNamePrefix: string) =
        typeNamePrefix + getIdentifierName nameFromSchema
    let private getSimpleTypeName (simpleType: SimpleType) (_typeNamePrefix: string) =
        match simpleType with
        | BinaryType (_width, _isFixed) -> "byte[]"
        | BooleanType -> "bool"
        | IntegerType -> "long"
        | LogicalType -> "bool"
        | NumberType -> "double"
        | RealType _precision -> "double"
        | StringType (_width, _isFixed) -> "string"
    let private generateSimpleType (simpleType: SimpleType): string option =
        let hasCustomTypeDefinintion =
            match simpleType with
            | _ -> false
        if hasCustomTypeDefinintion then
            match simpleType with
            | _ -> failwith "not supported"
        else None
    let rec private isBuiltInType (baseType: BaseType) (namedTypeOverrides: Map<string, BaseType>) =
        match baseType with
        | SimpleType _ -> true
        | NamedType n ->
            match Map.tryFind n namedTypeOverrides with
            | Some _ -> true
            | None -> false
        | _ -> false
    let rec private tryGetListTypeBounds (baseType: BaseType) (namedTypeOverrides: Map<string, BaseType>) =
        match baseType with
        | NamedType n ->
            match Map.tryFind n namedTypeOverrides with
            | Some namedTypeOverride -> tryGetListTypeBounds namedTypeOverride namedTypeOverrides
            | None -> None
        | AggregationType a ->
            match a with
            | ListType (innerType, (LiteralValue(IntegerLiteral(lower))), upperBoundOpt, _isUnique) ->
                match upperBoundOpt with
                | Some(LiteralValue(IntegerLiteral(upper))) -> Some(innerType, lower, Some upper)
                | None -> Some(innerType, lower, None)
                | _ -> None
            | _ -> None
        | _ -> None
    let rec getBaseTypeName (baseType: BaseType) (typeNamePrefix: string) (namedTypeOverrides: Map<string, BaseType>) =
        let rec getBaseTypeName' (baseType: BaseType) (typeNamePrefix: string) (namedTypeOverrides: Map<string, BaseType>) (normalizeName: bool) =
            match tryGetListTypeBounds baseType namedTypeOverrides with
            | Some(innerType, _lower, _upperOpt) ->
                let innerTypeName = getBaseTypeName' innerType typeNamePrefix namedTypeOverrides normalizeName
                $"ListWithMinimumAndMaximum<{innerTypeName}>"
            | None ->
                match baseType with
                | ConstructedType c ->
                    // Assuming `getConstructedTypeName` is a method to handle constructed types.
                    getConstructedTypeName c typeNamePrefix
                | AggregationType a ->
                    // Assuming `getAggregationTypeName` handles types like arrays/lists.
                    getAggregationTypeName a typeNamePrefix namedTypeOverrides
                | SimpleType s -> 
                    getSimpleTypeName s typeNamePrefix
                | NamedType n ->
                    match Map.tryFind n namedTypeOverrides with
                    | Some namedTypeOverride -> 
                        getBaseTypeName' namedTypeOverride "" namedTypeOverrides false
                    | None ->
                        if normalizeName then 
                            getIdentifierNameWithPrefix n typeNamePrefix
                        else n
        getBaseTypeName' baseType typeNamePrefix namedTypeOverrides true
        
    and getConstructedTypeName (c: ConstructedType) (typeNamePrefix: string) : string =
        match c with
        | EnumerationType names ->
            let combinedNames = String.concat ", " names // Create a CSV list of enumeration names
            $"enum %s{typeNamePrefix} {{ %s{combinedNames} }}"
        | SelectType types ->
            let typeNames = types |> List.map (fun t -> getBaseTypeName t typeNamePrefix Map.empty)
            let combinedTypes = String.concat " | " typeNames // Create a union type list
            $"type %s{typeNamePrefix} = %s{combinedTypes}"
       
    and getAggregationTypeName (a: AggregationType) (typeNamePrefix: string) (namedTypeOverrides: Map<string, BaseType>) : string =
        match a with
        | ArrayType (baseType, lower, upperOpt, isOptional, isUnique) ->
            let typeName = getBaseTypeName baseType typeNamePrefix namedTypeOverrides
            let suffix = if isOptional then "?" else ""
            $"%s{typeName}[]%s{suffix}"
        | BagType (baseType, lower, upperOpt) ->
            let typeName = getBaseTypeName baseType typeNamePrefix namedTypeOverrides
            sprintf "List<{typeName}>" // Assume Bag translates to a List without uniqueness constraints.
        | ListType (baseType, lower, upperOpt, isUnique) ->
           let typeName = getBaseTypeName baseType typeNamePrefix namedTypeOverrides
           let typeCollection = if isUnique then "HashSet" else "List"
           $"%s{typeCollection}<{{typeName}}>"
        | SetType (baseType, lower, upperOpt) ->
           let typeName = getBaseTypeName baseType typeNamePrefix namedTypeOverrides
           sprintf "HashSet<{typeName}>"

    let getNamedTypeOverride (typ: BaseType): BaseType option =
        match typ with
        | SimpleType s -> Some(NamedType(getSimpleTypeName s ""))
        | _ -> None
    let getNamedTypeOverrideMap (types: SchemaType list): Map<string, BaseType> =
        types
        |> List.map (fun t -> (t, getNamedTypeOverride t.Type))
        |> List.filter (snd >> Option.isSome)
        |> List.map (fun (a, b) -> (a.Name, Option.get b))
        |> Map.ofList
    let getExpressionCode (expression: Expression): string option =
        let rec getValidationStatementPredicate' (expression: Expression): string option =
            let testAndCombine (a: string option) (b: string option) (template: string): string option =
                match (a, b) with
                | (Some a, Some b) -> Some(String.Format(template, a, b))
                | _ -> None
            match expression with
            | LiteralValue l -> Some(l.ToString())
            | Identifier i ->
                match i.ToUpperInvariant() with
                | "SELF" -> "this"
                | _ -> getIdentifierName i
                |> Some
            | MemberAccess (a, b) -> testAndCombine (getValidationStatementPredicate' a) (b |> getIdentifierName |> Some) "{0}.{1}"
            | GroupAccess (a, b) -> testAndCombine (getValidationStatementPredicate' a) (b |> getIdentifierName |> Some) "{0}\\{1}"
            | Negate n -> 
                match getValidationStatementPredicate' n with
                | Some p -> Some("-" + p)
                | None -> None
            | Add (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} + {1})"
            | Subtract (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} - {1})"
            | Multiply (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} * {1})"
            | Divide (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} / {1})"
            | Modulus (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} % {1})"
            | Exponent (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "Math.Pow({0}, {1})"
            | Greater (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} > {1})"
            | GreaterEquals (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} >= {1})"
            | Less (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} < {1})"
            | LessEquals (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} <= {1})"
            | Equals (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} == {1})"
            | NotEquals (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} != {1})"
            | Assignable (a, b, _isStrict) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} is {1})"
            | NotAssignable (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "!({0} is {1})"
            | FunctionCallExpression f ->
                // check for well-known function names
                let clrFunction =
                    match f.Name.ToLowerInvariant() with
                    | "sqrt" -> Some "Math.Sqrt"
                    // the following are defined later in the schema, but for now we'll hard-code it
                    | "dimension_of" -> Some(getIdentifierName f.Name)
                    // not supported
                    | _ -> None
                match clrFunction with
                | Some clrFunction ->
                    let argumentSet =
                        f.Arguments
                        |> List.map getValidationStatementPredicate'
                        |> List.fold (fun state t ->
                            match (state, t) with
                            | (Some collection, Some value) -> Some(List.append collection [value])
                            | _ -> None) (Some [])
                    match argumentSet with
                    | Some arguments -> Some(sprintf "%s(%s)" clrFunction (arguments |> joinWith ", "))
                    | None -> None
                | None -> None
            | QueryExpression _ -> None // NYI
            | ArrayExpression _ -> None // NYI
            | SubcomponentQualifiedExpression _ -> failwith "subcomponent NYI"
            | In _ -> failwith "in NYI"
            | Or (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} || {1})"
            | Xor (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} ^ {1})"
            | And (a, b) -> testAndCombine (getValidationStatementPredicate' a) (getValidationStatementPredicate' b) "({0} && {1})"
            | Not n ->
                match getValidationStatementPredicate' n with
                | Some n -> Some $"!(%s{n})"
                | None -> None
        getValidationStatementPredicate' expression
    let private getExplicitAttributeDeclaration (attr: ExplicitAttribute) (typeNamePrefix: string) (namedTypeOverrides: Map<string, BaseType>) =
        let attributeType = getBaseTypeName attr.Type.Type typeNamePrefix namedTypeOverrides
        let attributeName = getIdentifierName attr.AttributeDeclaration.Name
        let fieldName = getFieldName attr.AttributeDeclaration.Name
        seq {
            match tryGetListTypeBounds attr.Type.Type namedTypeOverrides with
            | Some(innerType, lower, Some upper) ->
                let baseTypeName = getBaseTypeName innerType typeNamePrefix namedTypeOverrides
                yield $"public ListWithMinimumAndMaximum<{baseTypeName}> {attributeName} {{ get; }} = new ListWithMinimumAndMaximum<{baseTypeName}>({lower}, {upper});"
            | Some(innerType, lower, None) ->
                let baseTypeName = getBaseTypeName innerType typeNamePrefix namedTypeOverrides
                yield $"public ListWithMinimum<{baseTypeName}> {attributeName} {{ get; }} = new ListWithMinimum<{baseTypeName}>({lower});"
            | None ->
                yield $"public {getBaseTypeName} {attributeName} {{ get; set; }}"
                yield $"protected %s{attributeType} %s{fieldName};"
                yield $"public %s{attributeType} %s{attributeName}"
                yield "{"
                yield $"get => %s{fieldName};" |> indentLine
                yield "set" |> indentLine
                yield "{" |> indentLine
                yield $"%s{fieldName} = value;" |> indentLine |> indentLine
                yield "ValidateDomainRules();" |> indentLine |> indentLine
                yield "}" |> indentLine
                yield "}"
        } |> joinLines
    let private getDerivedAttributeDeclaration (attr: DerivedAttribute) (typeNamePrefix: string) (namedTypeOverrides: Map<string, BaseType>) =
        let attributeType = getBaseTypeName attr.Type.Type typeNamePrefix namedTypeOverrides
        let attributeName = getIdentifierName attr.AttributeDeclaration.Name
        let attributeExpression = getExpressionCode attr.Expression
        match attributeExpression with
        | Some ae -> $"public %s{attributeType} %s{attributeName} => %s{ae};"
        | None -> ""
    let private getValidationStatementBody (domainRule: DomainRule) =
        let predicate =
            match getExpressionCode domainRule.Expression with
            | Some p -> p
            | None -> "true /* TODO: not all validation predicates are supported */"
        let domainRuleText = domainRule.ToString()
        seq {
            yield $"if (!%s{predicate})"
            yield "{"
            yield sprintf "throw new StepValidationException(\"The validation rule '%s' was not satisfied\");" (domainRuleText.Replace("\"", "\\\"")) |> indentLine
            yield "}"
        } |> joinLines
    let private getDomainRuleValidationFunction (domainRules: DomainRule list) =
        let allDomainRules =
            domainRules
            |> List.map getValidationStatementBody
            |> List.map toLines
            |> Seq.concat
        seq {
            yield "internal override void ValidateDomainRules()"
            yield "{"
            yield "base.ValidateDomainRules();" |> indentLine
            yield! allDomainRules |> indentLines
            yield "}"
        } |> joinLines
    let rec private getEntityAndParentAttributes (schema: Schema) (entity: Entity) =
        let parentAttributes =
            match entity.SubTypes with
            | [] -> []
            | subTypeName::[] ->
                let parentEntity = schema.Entities |> List.find (fun e -> e.Name = subTypeName)
                let (p, pp) = getEntityAndParentAttributes schema parentEntity
                List.append p pp
            | _ -> failwith "multiple inheritance nyi"
        (parentAttributes, entity.Attributes)
    let private getReferencedItems (schema: Schema) (entity: Entity) (defaultBaseClassName: string) (namedTypeOverrides: Map<string, BaseType>) =
        seq {
            yield $"internal override IEnumerable<%s{defaultBaseClassName}> GetReferencedItems()"
            yield "{"
            yield! getEntityAndParentAttributes schema entity
                   |> snd
                   |> Seq.filter (fun attribute -> not (isBuiltInType attribute.Type.Type namedTypeOverrides))
                   |> Seq.map (fun attribute ->
                        seq {
                            let attributeName = getIdentifierName attribute.AttributeDeclaration.Name
                            match tryGetListTypeBounds attribute.Type.Type namedTypeOverrides with
                            | Some _ ->
                                // TODO: currently if it's a `List<T>` we don't return the sub-items
                                ()
                            | None ->
                                yield $"yield return %s{attributeName};"
                        })
                   |> Seq.concat
                   |> indentLines
            yield indentLine "yield break;"
            yield "}"
        } |> joinLines
    let private getParametersFunction (schema: Schema) (entity: Entity) =
        seq {
            yield "internal override IEnumerable<StepSyntax> GetParameters(StepWriter writer)"
            yield "{"
            yield! seq {
                yield "foreach (var parameter in base.GetParameters(writer))"
                yield "{"
                yield indentLine "yield return parameter;"
                yield "}"
            } |> indentLines
            yield ""
            yield! getEntityAndParentAttributes schema entity
                   |> snd
                   |> Seq.map (fun attribute -> $"yield return writer.GetItemSyntax(%s{getIdentifierName attribute.AttributeDeclaration.Name});")
                   |> indentLines
            yield "}"
        } |> joinLines
    let private getCreationFromSyntaxFunction (schema: Schema) (entity: Entity) (typeNamePrefix: string) (namedTypeOverrides: Map<string, BaseType>) =
        let entityName = getIdentifierNameWithPrefix entity.Name typeNamePrefix
        let (parentEntityAttributes, thisEntityAttributes) = getEntityAndParentAttributes schema entity
        let allAttributeCount = parentEntityAttributes.Length + thisEntityAttributes.Length
        let getSetterLinesFromAttributes (attributes: ExplicitAttribute list) (indexOffset: int) =
            attributes
            |> Seq.mapi (fun i e ->
                seq {
                    let fieldName = getFieldName e.AttributeDeclaration.Name
                    let index = i + indexOffset
                    match tryGetListTypeBounds e.Type.Type namedTypeOverrides with
                    | Some(innerType, lower, Some upper) ->
                        let attributeName = getIdentifierName e.AttributeDeclaration.Name
                        let baseTypeName = getBaseTypeName innerType typeNamePrefix namedTypeOverrides
                        yield $"syntaxList.Values[%d{index}].GetValueList().AssertListCount(%d{lower}, %d{upper});"
                        yield $"item.%s{attributeName}.AssignValues(syntaxList.Values[%d{index}].GetValueList().Values.Select(value => value.Get%s{getIdentifierName baseTypeName}Value()));"
                    // TODO: handle case where upper is None
                    | _ ->
                        let baseTypeName = getBaseTypeName e.Type.Type typeNamePrefix namedTypeOverrides
                        if isBuiltInType e.Type.Type namedTypeOverrides then
                            yield $"item.%s{fieldName} = syntaxList.Values[%d{index}].Get%s{getIdentifierName baseTypeName}Value();"
                        else
                            yield $"binder.BindValue(syntaxList.Values[%d{index}], value => item.%s{fieldName} = value.AsType<%s{baseTypeName}>());"
                })
            |> Seq.concat
        seq {
            yield $"internal static new %s{entityName} CreateFromSyntaxList(StepBinder binder, StepSyntaxList syntaxList)"
            yield "{"
            yield! seq {
                yield $"syntaxList.AssertListCount(%d{allAttributeCount});"
                yield $"var item = new %s{entityName}();"
                yield! getSetterLinesFromAttributes parentEntityAttributes 0
                yield! getSetterLinesFromAttributes thisEntityAttributes parentEntityAttributes.Length
                yield "return item;"
            } |> indentLines
            yield "}"
        } |> joinLines
    let getSchemaTypeName (schemaType: SchemaType) (typeNamePrefix: string) =
        match schemaType.Type with
        | SimpleType s -> getSimpleTypeName s typeNamePrefix
        | _ -> getIdentifierNameWithPrefix schemaType.Name typeNamePrefix
    let getSchemaTypeDefinition (schemaType: SchemaType) (typeNamePrefix: string): string option =
        let typeName = getSchemaTypeName schemaType typeNamePrefix
        match schemaType.Type with
        | ConstructedType c ->
            match c with
            | EnumerationType values ->
                let enumValues =
                    values
                    |> List.map getIdentifierName
                    |> List.map (fun v -> indentation + v + ",\n")
                    |> List.fold (+) ""
                Some $"public enum {typeName}\n{{\n{enumValues}}}\n"
            | SelectType types -> Some ""
        | AggregationType a -> Some ""
        | SimpleType s -> generateSimpleType s
        | NamedType n -> Some ""
    let getEntityDeclaration (entity: Entity) (typeNamePrefix: string) (defaultBaseClassName: string): string =
        let entityName = getIdentifierNameWithPrefix entity.Name typeNamePrefix
        let subTypeDefinitionText =
            match entity.SubTypes with
            | [] -> $" : %s{defaultBaseClassName}"
            | subTypeName::[] -> $" : %s{getIdentifierNameWithPrefix subTypeName typeNamePrefix}"
            | _ -> failwith "multiple inheritance NYI"
        $"public partial class %s{entityName}%s{subTypeDefinitionText}"
    let getEntityDefinition (schema: Schema) (entity: Entity) (generatedNamespace: string) (usingNamespaces: string seq) (typeNamePrefix: string) (defaultBaseClassName: string) (namedTypeOverrides: Map<string, BaseType>): (string * string) =
        let entityDeclaration = getEntityDeclaration entity typeNamePrefix defaultBaseClassName
        let explicitAttributeLines =
            entity.Attributes
            |> List.map (fun a -> getExplicitAttributeDeclaration a typeNamePrefix namedTypeOverrides)
            |> joinWith "\n\n"
            |> toLines
            |> indentLines
        let derivedAttributeLines =
            entity.DerivedAttributes
            |> List.map (fun a -> getDerivedAttributeDeclaration a typeNamePrefix namedTypeOverrides)
            |> joinWith "\n\n"
            |> toLines
            |> indentLines
        let allAttributeLines = Seq.append explicitAttributeLines derivedAttributeLines
        let validationRuleFunction = getDomainRuleValidationFunction entity.DomainRules |> toLines |> indentLines |> joinLines
        let getReferencedItems = getReferencedItems schema entity defaultBaseClassName namedTypeOverrides |> toLines |> indentLines |> joinLines
        let getParametersFunction = getParametersFunction schema entity |> toLines |> indentLines |> joinLines
        let entityCreationFunction = getCreationFromSyntaxFunction schema entity typeNamePrefix namedTypeOverrides |> toLines |> indentLines |> joinLines
        let (parentEntityAttributes, thisEntityAttributes) = getEntityAndParentAttributes schema entity
        let getAttributesAsArgumentTexts (attributes: ExplicitAttribute list) =
            let getArgumentType (baseType: BaseType) =
                match tryGetListTypeBounds baseType namedTypeOverrides with
                | Some(innerType, _lower, _upperOpt) ->
                    let innerTypeName = getBaseTypeName innerType typeNamePrefix namedTypeOverrides
                    $"IEnumerable<%s{innerTypeName}>"
                | _ -> getBaseTypeName baseType typeNamePrefix namedTypeOverrides
            attributes
            |> List.map (fun a -> $"%s{getArgumentType a.Type.Type} %s{a.AttributeDeclaration.Name |> getParameterName}")
        let thisArgumentTexts = getAttributesAsArgumentTexts thisEntityAttributes
        let parentArgumentTexts = getAttributesAsArgumentTexts parentEntityAttributes
        let allArgumentsText = List.append parentArgumentTexts thisArgumentTexts |> joinWith ", "
        let constructorLines =
            let entityName = getIdentifierNameWithPrefix entity.Name typeNamePrefix
            seq {
                yield $"internal %s{entityName}()"
                yield "{"
                yield "}"
                yield ""
                yield $"public %s{entityName}(%s{allArgumentsText})"
                yield "{"
                yield! Seq.append parentEntityAttributes thisEntityAttributes
                       |> Seq.map (fun attribute ->
                            let name = attribute.AttributeDeclaration.Name
                            let parameterName = getParameterName name
                            match tryGetListTypeBounds attribute.Type.Type namedTypeOverrides with
                            | Some _ ->
                                let attributeName = getIdentifierName name
                                $"%s{attributeName}.AssignValues(%s{parameterName});"
                            | _ ->
                                let fieldName = getFieldName name
                                $"%s{fieldName} = %s{parameterName};")
                       |> indentLines
                yield "ValidateDomainRules();" |> indentLine
                yield "}"
            } |> indentLines
        let generatedCode =
            seq {
                yield! usingNamespaces |> Seq.map (fun ns -> $"using %s{ns};")
                yield ""
                yield $"namespace %s{generatedNamespace}"
                yield "{"
                yield! entityDeclaration |> toLines |> indentLines
                yield "{" |> indentLine
                yield $"public override string ItemTypeString => \"%s{entity.Name.ToUpperInvariant()}\";" |> indentLine |> indentLine
                yield ""
                yield! allAttributeLines |> indentLines
                yield ""
                yield! constructorLines |> indentLines
                yield ""
                yield! validationRuleFunction |> toLines |> indentLines
                yield ""
                yield! getReferencedItems |> toLines |> indentLines
                yield ""
                yield! getParametersFunction |> toLines |> indentLines
                yield ""
                yield! entityCreationFunction |> toLines |> indentLines
                yield "}" |> indentLine
                yield "}"
                yield ""
            } |> joinLines
        let entityName = getIdentifierNameWithPrefix entity.Name typeNamePrefix
        (entityName, generatedCode)
    let getEntityDefinitions (schema: Schema) (generatedNamespace: string) (usingNamespaces: string seq) (typeNamePrefix: string) (defaultBaseClassName: string): (string * string) list =
        let namedTypeOverrides = getNamedTypeOverrideMap schema.Types
        schema.Entities
        |> List.map (fun e -> getEntityDefinition schema e generatedNamespace usingNamespaces typeNamePrefix defaultBaseClassName namedTypeOverrides)
    let getFromItemSyntaxFile (schema: Schema) (generatedNamespace: string) (typeNamePrefix: string) (defaultBaseClassName: string): (string * string) =
        let itemName = $"%s{defaultBaseClassName}Builder"
        let content =
            seq {
                yield "using IxMilia.Step.Syntax;"
                yield ""
                yield $"namespace %s{generatedNamespace}"
                yield "{"
                yield! seq {
                    yield $"internal static class %s{itemName}"
                    yield "{"
                    yield! seq {
                        yield $"internal static %s{defaultBaseClassName} FromTypedParameter(StepBinder binder, StepItemSyntax itemSyntax)"
                        yield "{"
                        yield! seq {
                            yield $"%s{defaultBaseClassName} item = null;"
                            yield "if (itemSyntax is StepSimpleItemSyntax simpleItem)"
                            yield "{"
                            yield! seq {
                                yield "switch (simpleItem.Keyword.ToUpperInvariant())"
                                yield "{"
                                yield! schema.Entities
                                        |> Seq.map (fun entity ->
                                            seq {
                                                yield $"case \"%s{entity.Name.ToUpperInvariant()}\":"
                                                yield! seq {
                                                    yield $"item = %s{getIdentifierNameWithPrefix entity.Name typeNamePrefix}.CreateFromSyntaxList(binder, simpleItem.Parameters);"
                                                    yield "break;"
                                                } |> indentLines;
                                            })
                                        |> Seq.concat
                                        |> indentLines
                                yield! seq {
                                    yield "default:"
                                    yield! seq {
                                        yield "default:"
                                        yield! seq {
                                            yield "log.Error(\"Unsupported item encountered.\");"
                                            yield "break;"
                                        } |> indentLines
                                        yield "break;"
                                    } |> indentLines
                                } |> indentLines
                                yield "}"
                            } |> indentLines
                            yield "}"
                            yield "// TODO: else"
                            yield ""
                            yield "return item;"
                        } |> indentLines
                        yield "}"
                    } |> indentLines
                    yield "}"
                } |> indentLines
                yield "}"
                yield ""
            } |> joinLines
        (itemName, content)
    let getAllFileDefinitions (schema: Schema) (generatedNamespace: string) (usingNamespaces: string seq) (typeNamePrefix: string) (defaultBaseClassName: string): (string * string) list =
        seq {
            yield getFromItemSyntaxFile schema generatedNamespace typeNamePrefix defaultBaseClassName
            yield! getEntityDefinitions schema generatedNamespace usingNamespaces typeNamePrefix defaultBaseClassName
        } |> List.ofSeq
