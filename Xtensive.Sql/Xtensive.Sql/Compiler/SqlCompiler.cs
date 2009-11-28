// Copyright (C) 2007 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.

using System;
using System.Collections.Generic;
using System.Linq;
using Xtensive.Core;
using Xtensive.Core.Internals.DocTemplates;
using Xtensive.Sql.Info;
using Xtensive.Sql.Model;
using Xtensive.Sql.Ddl;
using Xtensive.Sql.Dml;
using Xtensive.Sql.Exceptions;
using Xtensive.Sql.Resources;

namespace Xtensive.Sql.Compiler
{
  public class SqlCompiler : ISqlVisitor
  {
    protected readonly SqlValueType decimalType;
    protected readonly SqlTranslator translator;
    protected readonly SqlDriver driver;
    
    protected SqlCompilerConfiguration configuration;
    protected SqlCompilerContext context;

    public SqlCompilationResult Compile(ISqlCompileUnit unit, SqlCompilerConfiguration compilerConfiguration)
    {
      ArgumentValidator.EnsureArgumentNotNull(unit, "unit");
      configuration = compilerConfiguration;
      context = new SqlCompilerContext(configuration);
      unit.AcceptVisitor(this);
      return new SqlCompilationResult(
        Compressor.Process(translator, context.Output),
        context.ParameterNameProvider.NameTable);
    }

    public virtual void Visit(SqlAggregate node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Expression.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }
    
    public virtual void Visit(SqlAlterDomain node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, AlterDomainSection.Entry));
        if (node.Action is SqlAddConstraint) {
          var constraint = (DomainConstraint) ((SqlAddConstraint)node.Action).Constraint;
          context.Output.AppendText(translator.Translate(context, node, AlterDomainSection.AddConstraint));
          context.Output.AppendText(translator.Translate(context, constraint, ConstraintSection.Entry));
          context.Output.AppendText(translator.Translate(context, constraint, ConstraintSection.Check));
          constraint.Condition.AcceptVisitor(this);
          context.Output.AppendText(translator.Translate(context, constraint, ConstraintSection.Exit));
        }
        else if (node.Action is SqlDropConstraint) {
          var action = node.Action as SqlDropConstraint;
          context.Output.AppendText(translator.Translate(context, node, AlterDomainSection.DropConstraint));
          context.Output.AppendText(translator.Translate(context, action.Constraint, ConstraintSection.Entry));
        }
        else if (node.Action is SqlSetDefault) {
          var action = node.Action as SqlSetDefault;
          context.Output.AppendText(translator.Translate(context, node, AlterDomainSection.SetDefault));
          action.DefaultValue.AcceptVisitor(this);
        }
        else if (node.Action is SqlDropDefault)
          context.Output.AppendText(translator.Translate(context, node, AlterDomainSection.DropDefault));
        context.Output.AppendText(translator.Translate(context, node, AlterDomainSection.Exit));
      }
    }

    public virtual void Visit(SqlAlterPartitionFunction node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlAlterPartitionScheme node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlAlterTable node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, AlterTableSection.Entry));
        if (node.Action is SqlAddColumn) {
          var column = ((SqlAddColumn) node.Action).Column;
          context.Output.AppendText(translator.Translate(context, node, AlterTableSection.AddColumn));
          Visit(column);
        }
        else if (node.Action is SqlDropDefault) {
          var column = ((SqlDropDefault) node.Action).Column;
          context.Output.AppendText(translator.Translate(context, node, AlterTableSection.AlterColumn));
          context.Output.AppendText(translator.Translate(context, column, TableColumnSection.Entry));
          context.Output.AppendText(translator.Translate(context, column, TableColumnSection.DropDefault));
        }
        else if (node.Action is SqlSetDefault) {
          var action = node.Action as SqlSetDefault;
          context.Output.AppendText(translator.Translate(context, node, AlterTableSection.AlterColumn));
          context.Output.AppendText(translator.Translate(context, action.Column, TableColumnSection.Entry));
          context.Output.AppendText(translator.Translate(context, action.Column, TableColumnSection.SetDefault));
          action.DefaultValue.AcceptVisitor(this);
        }
        else if (node.Action is SqlDropColumn) {
          var action = node.Action as SqlDropColumn;
          context.Output.AppendText(translator.Translate(context, node, AlterTableSection.DropColumn));
          context.Output.AppendText(translator.Translate(context, action.Column, TableColumnSection.Entry));
          context.Output.AppendText(translator.Translate(context, node, AlterTableSection.DropBehavior));
        }
        else if (node.Action is SqlAlterIdentityInfo) {
          var action = node.Action as SqlAlterIdentityInfo;
          context.Output.AppendText(translator.Translate(context, node, AlterTableSection.AlterColumn));
          context.Output.AppendText(translator.Translate(context, action.Column, TableColumnSection.Entry));
          if ((action.InfoOption & SqlAlterIdentityInfoOptions.RestartWithOption)!=0)
            context.Output.AppendText(translator.Translate(context, action.SequenceDescriptor, SequenceDescriptorSection.RestartValue));
          if ((action.InfoOption & SqlAlterIdentityInfoOptions.IncrementByOption)!=0) {
            if (action.SequenceDescriptor.Increment.HasValue && action.SequenceDescriptor.Increment.Value==0)
              throw new SqlCompilerException("Increment must not be 0.");
            context.Output.AppendText(translator.Translate(context, action.Column, TableColumnSection.SetIdentityInfoElement));
            context.Output.AppendText(translator.Translate(context, action.SequenceDescriptor, SequenceDescriptorSection.Increment));
          }
          if ((action.InfoOption & SqlAlterIdentityInfoOptions.MaxValueOption)!=0) {
            context.Output.AppendText(translator.Translate(context, action.Column, TableColumnSection.SetIdentityInfoElement));
            context.Output.AppendText(translator.Translate(context, action.SequenceDescriptor, SequenceDescriptorSection.AlterMaxValue));
          }
          if ((action.InfoOption & SqlAlterIdentityInfoOptions.MinValueOption)!=0) {
            context.Output.AppendText(translator.Translate(context, action.Column, TableColumnSection.SetIdentityInfoElement));
            context.Output.AppendText(translator.Translate(context, action.SequenceDescriptor, SequenceDescriptorSection.AlterMinValue));
          }
          if ((action.InfoOption & SqlAlterIdentityInfoOptions.CycleOption)!=0) {
            context.Output.AppendText(translator.Translate(context, action.Column, TableColumnSection.SetIdentityInfoElement));
            context.Output.AppendText(translator.Translate(context, action.SequenceDescriptor, SequenceDescriptorSection.IsCyclic));
          }
        }
        else if (node.Action is SqlAddConstraint) {
          var constraint = ((SqlAddConstraint) node.Action).Constraint as TableConstraint;
          context.Output.AppendText(translator.Translate(context, node, AlterTableSection.AddConstraint));
          Visit(constraint);
        }
        else if (node.Action is SqlDropConstraint) {
          var action = node.Action as SqlDropConstraint;
          var constraint = action.Constraint as TableConstraint;
          context.Output.AppendText(translator.Translate(context, node, AlterTableSection.DropConstraint));
          context.Output.AppendText(translator.Translate(context, constraint, ConstraintSection.Entry));
          context.Output.AppendText(translator.Translate(context, node, AlterTableSection.DropBehavior));
        }
        else if (node.Action is SqlRenameColumn) {
          var action = node.Action as SqlRenameColumn;
          context.Output.AppendText(translator.Translate(context, node, AlterTableSection.RenameColumn));
          context.Output.AppendText(translator.Translate(context, action.Column, TableColumnSection.Entry));
          context.Output.AppendText(translator.Translate(context, node, AlterTableSection.To));
          context.Output.AppendText(translator.QuoteIdentifier(action.NewName));
        }
        context.Output.AppendText(translator.Translate(context, node, AlterTableSection.Exit));
      }
    }

    public virtual void Visit(SqlAlterSequence node)
    {
      context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
      if ((node.InfoOption & SqlAlterIdentityInfoOptions.RestartWithOption)!=0)
        context.Output.AppendText(translator.Translate(context, node.SequenceDescriptor, SequenceDescriptorSection.RestartValue));
      if ((node.InfoOption & SqlAlterIdentityInfoOptions.IncrementByOption)!=0) {
        if (node.SequenceDescriptor.Increment.HasValue && node.SequenceDescriptor.Increment.Value==0)
          throw new SqlCompilerException("Increment must not be 0.");
        context.Output.AppendText(translator.Translate(context, node.SequenceDescriptor, SequenceDescriptorSection.Increment));
      }
      if ((node.InfoOption & SqlAlterIdentityInfoOptions.MaxValueOption)!=0)
        context.Output.AppendText(translator.Translate(context, node.SequenceDescriptor, SequenceDescriptorSection.AlterMaxValue));
      if ((node.InfoOption & SqlAlterIdentityInfoOptions.MinValueOption)!=0)
        context.Output.AppendText(translator.Translate(context, node.SequenceDescriptor, SequenceDescriptorSection.AlterMinValue));
      if ((node.InfoOption & SqlAlterIdentityInfoOptions.CycleOption)!=0)
        context.Output.AppendText(translator.Translate(context, node.SequenceDescriptor, SequenceDescriptorSection.IsCyclic));
      context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
    }

    public virtual void Visit(SqlArray node)
    {
      var itemType = node.ItemType;
      var items = node.GetValues();
      if (items.Length==0) {
        context.Output.AppendText(translator.Translate(context, node, ArraySection.EmptyArray));
        return;
      }
      context.Output.AppendText(translator.Translate(context, node, ArraySection.Entry));
      for (int i = 0; i < items.Length-1; i++) {
        context.Output.AppendText(translator.Translate(context, itemType, items[i]));
        context.Output.AppendDelimiter(translator.RowItemDelimiter);
      }
      context.Output.AppendText(translator.Translate(context, itemType, items[items.Length-1]));
      context.Output.AppendText(translator.Translate(context, node, ArraySection.Exit));
    }

    public virtual void Visit(SqlAssignment node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Left.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(node.NodeType));
        node.Right.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlBatch node)
    {
      using (context.EnterScope(node)) {
        var statements = FlattenBatch(node).ToArray();
        if (statements.Length==0)
          return;
        if (statements.Length==1) {
          statements[0].AcceptVisitor(this);
          return;
        }
        context.Output.AppendText(translator.BatchBegin);
        using (context.EnterCollectionScope()) {
          foreach (var item in statements) {
            item.AcceptVisitor(this);
            context.Output.AppendDelimiter(translator.BatchItemDelimiter, SqlDelimiterType.Column);
          }
        }
        context.Output.AppendText(translator.BatchEnd);
      }
    }

    public virtual void Visit(SqlBetween node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, BetweenSection.Entry));
        node.Expression.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, BetweenSection.Between));
        node.Left.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, BetweenSection.And));
        node.Right.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, BetweenSection.Exit));
      }
    }

    public virtual void Visit(SqlBinary node)
    {
      if (node.NodeType==SqlNodeType.In || node.NodeType==SqlNodeType.NotIn) {
        var row = node.Right as SqlRow;
        var array = node.Right as SqlArray;
        bool emptyIn =
          !row.IsNullReference() && row.Count==0 ||
          !array.IsNullReference() && array.GetValues().Length==0;
        if (emptyIn) {
          SqlDml.Literal(node.NodeType==SqlNodeType.NotIn).AcceptVisitor(this);
          return;
        }
      }

      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Left.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(node.NodeType));
        node.Right.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlBreak node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCase node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, CaseSection.Entry));

        if (!node.Value.IsNullReference()) {
          context.Output.AppendText(translator.Translate(context, node, CaseSection.Value));
          node.Value.AcceptVisitor(this);
        }

        using (context.EnterCollectionScope()) {
          foreach (KeyValuePair<SqlExpression, SqlExpression> item in node) {
            if (!context.IsEmpty)
              context.Output.AppendDelimiter(translator.WhenDelimiter);
            context.Output.AppendText(translator.Translate(context, node, item.Key, CaseSection.When));
            item.Key.AcceptVisitor(this);
            context.Output.AppendText(translator.Translate(context, node, item.Value, CaseSection.Then));
            item.Value.AcceptVisitor(this);
          }
        }

        if (!node.Else.IsNullReference()) {
          context.Output.AppendText(translator.Translate(context, node, CaseSection.Else));
          node.Else.AcceptVisitor(this);
        }

        context.Output.AppendText(translator.Translate(context, node, CaseSection.Exit));
      }
    }

    public virtual void Visit(SqlCast node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Operand.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlCloseCursor node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCollate node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Operand.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlColumnRef node)
    {
      context.Output.AppendText(translator.Translate(context, node, ColumnSection.Entry));
    }

    public virtual void Visit(SqlNative node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlNativeHint node)
    {
      // nothing
    }

    public virtual void Visit(SqlConcat node)
    {
      context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
      bool first = true;
      foreach (var item in node) {
        if (!first) {
          context.Output.AppendText(translator.Translate(node.NodeType));
        }
        item.AcceptVisitor(this);
        first = false;
      }

      context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
    }

    public virtual void Visit(SqlContainer node)
    {
      throw new SqlCompilerException(Strings.ExSqlContainerExpressionCanNotBeCompiled);
    }

    public virtual void Visit(SqlContinue node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCommand node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCreateAssertion node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Assertion.Condition.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlCreateCharacterSet node)
    {
//      ArgumentValidator.EnsureArgumentNotNull(node.CharacterSet.CharacterSetSource, "CharacterSetSource");
//      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCreateCollation node)
    {
//      ArgumentValidator.EnsureArgumentNotNull(node.Collation.CharacterSet, "CharacterSet");
//      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCreateDomain node)
    {
      ArgumentValidator.EnsureArgumentNotNull(node.Domain.DataType, "DataType");
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, CreateDomainSection.Entry));
        if (!node.Domain.DefaultValue.IsNullReference()) {
          context.Output.AppendText(translator.Translate(context, node, CreateDomainSection.DomainDefaultValue));
          node.Domain.DefaultValue.AcceptVisitor(this);
        }
        if (node.Domain.DomainConstraints.Count!=0) {
          using (context.EnterCollectionScope())
            foreach (DomainConstraint constraint in node.Domain.DomainConstraints) {
              context.Output.AppendText(translator.Translate(context, constraint, ConstraintSection.Entry));
              context.Output.AppendText(translator.Translate(context, constraint, ConstraintSection.Check));
              constraint.Condition.AcceptVisitor(this);
              context.Output.AppendText(translator.Translate(context, constraint, ConstraintSection.Exit));
            }
        }
        if (node.Domain.Collation!=null)
          context.Output.AppendText(translator.Translate(context, node, CreateDomainSection.DomainCollate));
        context.Output.AppendText(translator.Translate(context, node, CreateDomainSection.Exit));
      }
    }

    public virtual void Visit(SqlCreateIndex node)
    {
      ArgumentValidator.EnsureArgumentNotNull(node.Index.DataTable, "DataTable");
      context.Output.AppendText(translator.Translate(context, node, CreateIndexSection.Entry));
      if (node.Index.Columns.Count > 0) {
        context.Output.AppendText(translator.Translate(context, node, CreateIndexSection.ColumnsEnter));
        using (context.EnterCollectionScope()) {
          foreach (var item in node.Index.Columns) {
            if (!context.IsEmpty)
              context.Output.AppendDelimiter(translator.ColumnDelimiter);
            if (!item.Expression.IsNullReference() && driver.ServerInfo.Index.Features.Supports(IndexFeatures.Expressions))
              using (context.EnterScope(context.NamingOptions & ~SqlCompilerNamingOptions.TableQualifiedColumns)) {
                item.Expression.AcceptVisitor(this);
              }
            else {
              context.Output.AppendText(translator.QuoteIdentifier(item.Column.Name));
              if (driver.ServerInfo.Index.Features.Supports(IndexFeatures.SortOrder))
                context.Output.AppendText(translator.TranslateSortOrder(item.Ascending));
            }
          }
        }
        context.Output.AppendText(translator.Translate(context, node, CreateIndexSection.ColumnsExit));
      }
      if (node.Index.NonkeyColumns != null && node.Index.NonkeyColumns.Count != 0 && driver.ServerInfo.Index.Features.Supports(IndexFeatures.NonKeyColumns)) {
        context.Output.AppendText(translator.Translate(context, node, CreateIndexSection.NonkeyColumnsEnter));
        using (context.EnterCollectionScope()) {
          foreach (var item in node.Index.NonkeyColumns) {
            if (!context.IsEmpty)
              context.Output.AppendDelimiter(translator.ColumnDelimiter);
            context.Output.AppendText(translator.QuoteIdentifier(item.Name));
          }
        }
        context.Output.AppendText(translator.Translate(context, node, CreateIndexSection.NonkeyColumnsExit));
      }

      context.Output.AppendText(translator.Translate(context, node, CreateIndexSection.StorageOptions));

      if (!node.Index.Where.IsNullReference() && driver.ServerInfo.Index.Features.Supports(IndexFeatures.Filtered)) {
        context.Output.AppendText(translator.Translate(context, node, CreateIndexSection.Where));
        using (context.EnterScope(context.NamingOptions & ~SqlCompilerNamingOptions.TableQualifiedColumns)) {
          node.Index.Where.AcceptVisitor(this);
        }
      }
      context.Output.AppendText(translator.Translate(context, node, CreateIndexSection.Exit));
    }

    public virtual void Visit(SqlCreatePartitionFunction node)
    {
      ArgumentValidator.EnsureArgumentNotNull(node.PartitionFunction.DataType, "DataType");
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCreatePartitionScheme node)
    {
      ArgumentValidator.EnsureArgumentNotNull(node.PartitionSchema.PartitionFunction, "PartitionFunction");
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCreateSchema node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        context.Output.AppendDelimiter(translator.DdlStatementDelimiter, SqlDelimiterType.Column);
        if (node.Schema.Assertions.Count>0)
          using (context.EnterCollectionScope())
            foreach (Assertion assertion in node.Schema.Assertions) {
              new SqlCreateAssertion(assertion).AcceptVisitor(this);
              context.Output.AppendDelimiter(translator.DdlStatementDelimiter, SqlDelimiterType.Column);
            }

        if (node.Schema.CharacterSets.Count>0)
          using (context.EnterCollectionScope())
            foreach (CharacterSet characterSet in node.Schema.CharacterSets) {
              new SqlCreateCharacterSet(characterSet).AcceptVisitor(this);
              context.Output.AppendDelimiter(translator.DdlStatementDelimiter, SqlDelimiterType.Column);
            }

        if (node.Schema.Collations.Count>0)
          using (context.EnterCollectionScope())
            foreach (Collation collation in node.Schema.Collations) {
              if (!context.IsEmpty)
                context.Output.AppendDelimiter(translator.DdlStatementDelimiter, SqlDelimiterType.Column);
              new SqlCreateCollation(collation).AcceptVisitor(this);
            }

        if (node.Schema.Domains.Count>0)
          using (context.EnterCollectionScope())
            foreach (Domain domain in node.Schema.Domains) {
              new SqlCreateDomain(domain).AcceptVisitor(this);
              context.Output.AppendDelimiter(translator.DdlStatementDelimiter, SqlDelimiterType.Column);
            }

        if (node.Schema.Sequences.Count>0)
          using (context.EnterCollectionScope())
            foreach (Sequence sequence in node.Schema.Sequences) {
              new SqlCreateSequence(sequence).AcceptVisitor(this);
              context.Output.AppendDelimiter(translator.DdlStatementDelimiter, SqlDelimiterType.Column);
            }

        if (node.Schema.Tables.Count>0)
          using (context.EnterCollectionScope())
            foreach (Table table in node.Schema.Tables) {
              new SqlCreateTable(table).AcceptVisitor(this);
              context.Output.AppendDelimiter(translator.DdlStatementDelimiter, SqlDelimiterType.Column);
              if (table.Indexes.Count>0)
                using (context.EnterCollectionScope())
                  foreach (Index index in table.Indexes) {
                    new SqlCreateIndex(index).AcceptVisitor(this);
                    context.Output.AppendDelimiter(translator.DdlStatementDelimiter, SqlDelimiterType.Column);
                  }
            }

        if (node.Schema.Translations.Count>0)
          using (context.EnterCollectionScope())
            foreach (Translation translation in node.Schema.Translations) {
              new SqlCreateTranslation(translation).AcceptVisitor(this);
              context.Output.AppendDelimiter(translator.DdlStatementDelimiter, SqlDelimiterType.Column);
            }

        if (node.Schema.Views.Count>0)
          using (context.EnterCollectionScope())
            foreach (View view in node.Schema.Views) {
              new SqlCreateView(view).AcceptVisitor(this);
              context.Output.AppendDelimiter(translator.DdlStatementDelimiter, SqlDelimiterType.Column);
            }

        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }
    
    public virtual void Visit(SqlCreateSequence node)
    {
      ArgumentValidator.EnsureArgumentNotNull(node.Sequence.DataType, "DataType");
      ArgumentValidator.EnsureArgumentNotNull(node.Sequence.SequenceDescriptor, "SequenceDescriptor");
      using (context.EnterScope(node)) {
        var dataType = node.Sequence.DataType;
        if (!SqlValueType.IsExactNumeric(dataType) || dataType.Scale.HasValue && dataType.Scale!=0)
          throw new SqlCompilerException("The data type must be exact numeric without scale or with zero scale.");
        if (node.Sequence.SequenceDescriptor.Increment.HasValue && node.Sequence.SequenceDescriptor.Increment.Value==0)
            throw new SqlCompilerException("Increment must not be 0.");
        if (String.IsNullOrEmpty(node.Sequence.Name))
          throw new SqlCompilerException("Name must be not null or empty.");
        if (node.Sequence.Schema==null)
          throw new SqlCompilerException("Schema must be not null.");
        if (node.Sequence.SequenceDescriptor.MaxValue.HasValue &&
            node.Sequence.SequenceDescriptor.MinValue.HasValue &&
            node.Sequence.SequenceDescriptor.MaxValue.Value<=node.Sequence.SequenceDescriptor.MinValue.Value)
          throw new SqlCompilerException("The maximum value must be greater than the minimum value.");
        if (node.Sequence.SequenceDescriptor.StartValue.HasValue &&
            (node.Sequence.SequenceDescriptor.MaxValue.HasValue &&
             node.Sequence.SequenceDescriptor.MaxValue.Value<node.Sequence.SequenceDescriptor.StartValue.Value ||
             node.Sequence.SequenceDescriptor.MinValue.HasValue &&
             node.Sequence.SequenceDescriptor.MinValue.Value>node.Sequence.SequenceDescriptor.StartValue.Value))
          throw new SqlCompilerException("The start value must lie between the minimum and maximum value.");
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        context.Output.AppendText(translator.Translate(context, node.Sequence.SequenceDescriptor, SequenceDescriptorSection.StartValue));
        context.Output.AppendText(translator.Translate(context, node.Sequence.SequenceDescriptor, SequenceDescriptorSection.Increment));
        context.Output.AppendText(translator.Translate(context, node.Sequence.SequenceDescriptor, SequenceDescriptorSection.MaxValue));
        context.Output.AppendText(translator.Translate(context, node.Sequence.SequenceDescriptor, SequenceDescriptorSection.MinValue));
        context.Output.AppendText(translator.Translate(context, node.Sequence.SequenceDescriptor, SequenceDescriptorSection.IsCyclic));
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlCreateTable node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, CreateTableSection.Entry));
        context.Output.AppendText(translator.Translate(context, node, CreateTableSection.TableElementsEntry));

        bool firstWasProcessed = false;
        if (node.Table.Columns.Count>0)
          using (context.EnterCollectionScope()) {
            foreach (TableColumn c in node.Table.Columns) {
              // Skipping computed columns
              if (!c.Expression.IsNullReference() && !driver.ServerInfo.Column.Features.Supports(ColumnFeatures.Computed))
                continue;
              if (firstWasProcessed)
                context.Output.AppendDelimiter(translator.ColumnDelimiter, SqlDelimiterType.Column);
              else
                firstWasProcessed = true;
              Visit(c);
            }
          }

        if (node.Table.TableConstraints.Count>0)
          using (context.EnterCollectionScope()) {
            foreach (TableConstraint cs in node.Table.TableConstraints) {
              if (firstWasProcessed)
                context.Output.AppendDelimiter(translator.ColumnDelimiter, SqlDelimiterType.Column);
              else
                firstWasProcessed = true;
              Visit(cs);
            }
          }
        context.Output.AppendText(translator.Translate(context, node, CreateTableSection.TableElementsExit));
        if (node.Table.PartitionDescriptor!=null) {
          context.Output.AppendDelimiter(translator.ColumnDelimiter, SqlDelimiterType.Column);
          context.Output.AppendText(translator.Translate(context, node, CreateTableSection.Partition));
        }
        context.Output.AppendText(translator.Translate(context, node, CreateTableSection.Exit));
      }
    }

    public virtual void Visit(SqlCreateTranslation node)
    {
//      ArgumentValidator.EnsureArgumentNotNull(node.Translation.SourceCharacterSet, "SourceCharacterSet");
//      ArgumentValidator.EnsureArgumentNotNull(node.Translation.TargetCharacterSet, "TargetCharacterSet");
//      ArgumentValidator.EnsureArgumentNotNull(node.Translation.TranslationSource, "TranslationSource");
//      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCreateView node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        if (!node.View.Definition.IsNullReference())
          node.View.Definition.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlCursor node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDeclareCursor node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, DeclareCursorSection.Entry));
        context.Output.AppendText(translator.Translate(context, node, DeclareCursorSection.Sensivity));
        context.Output.AppendText(translator.Translate(context, node, DeclareCursorSection.Scrollability));
        context.Output.AppendText(translator.Translate(context, node, DeclareCursorSection.Cursor));
        context.Output.AppendText(translator.Translate(context, node, DeclareCursorSection.Holdability));
        context.Output.AppendText(translator.Translate(context, node, DeclareCursorSection.Returnability));
        context.Output.AppendText(translator.Translate(context, node, DeclareCursorSection.For));

        node.Cursor.Query.AcceptVisitor(this);
        if (node.Cursor.Columns.Count!=0) {
          foreach (SqlColumnRef item in node.Cursor.Columns) {
            item.AcceptVisitor(this);
          }
        }
        context.Output.AppendText(translator.Translate(context, node, DeclareCursorSection.Updatability));
        context.Output.AppendText(translator.Translate(context, node, DeclareCursorSection.Exit));
      }
    }

    public virtual void Visit(SqlDeclareVariable node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDefaultValue node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDelete node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, DeleteSection.Entry));
        if (node.From==null)
          throw new SqlCompilerException(Strings.ExTablePropertyIsNotSet);

        using (context.EnterScope(context.NamingOptions & ~SqlCompilerNamingOptions.TableAliasing)) {
          node.From.AcceptVisitor(this);
        }

        if (!node.Where.IsNullReference()) {
            context.Output.AppendText(translator.Translate(context, node, DeleteSection.Where));
            node.Where.AcceptVisitor(this);
        }
        context.Output.AppendText(translator.Translate(context, node, DeleteSection.Exit));
      }
    }

    public virtual void Visit(SqlDropAssertion node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropCharacterSet node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropCollation node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropDomain node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropIndex node)
    {
      ArgumentValidator.EnsureArgumentNotNull(node.Index.DataTable, "DataTable");
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropPartitionFunction node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropPartitionScheme node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropSchema node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropSequence node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropTable node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropTranslation node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropView node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlFastFirstRowsHint node)
    {
      // nothing
    }

    public virtual void Visit(SqlDynamicFilter node)
    {
      switch (node.Expressions.Count) {
      case 0:
        SqlDml.Literal(true).AcceptVisitor(this);
        break;
      case 1:
        TranslateDynamicFilterViaInOperator(node);
        break;
      default:
        if (driver.ServerInfo.Query.Features.Supports(QueryFeatures.MulticolumnIn))
          TranslateDynamicFilterViaInOperator(node);
        else
          TranslateDynamicFilterViaMultipleComparisons(node);
        break;
      }
    }

    public virtual void Visit(SqlFetch node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, FetchSection.Entry));
        if (!node.RowCount.IsNullReference())
          node.RowCount.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, FetchSection.Targets));
        foreach (ISqlCursorFetchTarget item in node.Targets) {
          item.AcceptVisitor(this);
        }
        context.Output.AppendText(translator.Translate(context, node, FetchSection.Exit));
      }
    }

    public virtual void Visit(SqlForceJoinOrderHint node)
    {
      // nothing
    }

    public virtual void Visit(SqlFunctionCall node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, FunctionCallSection.Entry, -1));
        if (node.Arguments.Count>0) {
          using (context.EnterCollectionScope()) {
            int argumentPosition = 0;
            foreach (SqlExpression item in node.Arguments) {
              if (!context.IsEmpty)
                context.Output.AppendDelimiter(translator.Translate(context, node, FunctionCallSection.ArgumentDelimiter, argumentPosition));
              context.Output.AppendText(translator.Translate(context, node, FunctionCallSection.ArgumentEntry, argumentPosition));
              item.AcceptVisitor(this);
              context.Output.AppendText(translator.Translate(context, node, FunctionCallSection.ArgumentExit, argumentPosition));
              argumentPosition++;
            }
          }
        }
        context.Output.AppendText(translator.Translate(context, node, FunctionCallSection.Exit, -1));
      }
    }

    public virtual void Visit(SqlIf node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, IfSection.Entry));

        node.Condition.AcceptVisitor(this);

        context.Output.AppendText(translator.Translate(context, node, IfSection.True));
        node.True.AcceptVisitor(this);

        if (node.False!=null) {
          context.Output.AppendText(translator.Translate(context, node, IfSection.False));
          node.False.AcceptVisitor(this);
        }

        context.Output.AppendText(translator.Translate(context, node, IfSection.Exit));
      }
    }

    public virtual void Visit(SqlInsert node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, InsertSection.Entry));

        if (node.Into==null)
          throw new SqlCompilerException(Strings.ExTablePropertyIsNotSet);

        using (context.EnterScope(context.NamingOptions & ~SqlCompilerNamingOptions.TableAliasing)) {
          node.Into.AcceptVisitor(this);
        }

        context.Output.AppendText(translator.Translate(context, node, InsertSection.ColumnsEntry));
        if (node.Values.Keys.Count > 0)
          using (context.EnterCollectionScope())
            foreach (SqlColumn item in node.Values.Keys) {
              if (!context.IsEmpty)
                context.Output.AppendDelimiter(translator.ColumnDelimiter);
              context.Output.AppendText(translator.QuoteIdentifier(item.Name));
            }
        context.Output.AppendText(translator.Translate(context, node, InsertSection.ColumnsExit));

        if (node.Values.Keys.Count == 0)
          context.Output.AppendText(translator.Translate(context, node, InsertSection.DefaultValues));
        else {
          context.Output.AppendText(translator.Translate(context, node, InsertSection.ValuesEntry));
          using (context.EnterCollectionScope())
            foreach (SqlExpression item in node.Values.Values) {
              if (!context.IsEmpty)
                context.Output.AppendDelimiter(translator.ColumnDelimiter);
              item.AcceptVisitor(this);
            }
          context.Output.AppendText(translator.Translate(context, node, InsertSection.ValuesExit));
        }
        context.Output.AppendText(translator.Translate(context, node, InsertSection.Exit));
      }
    }

    public virtual void Visit(SqlJoinExpression node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, JoinSection.Entry));
        node.Left.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, JoinSection.Specification));
        node.Right.AcceptVisitor(this);
        if (!node.Expression.IsNullReference()) {
          context.Output.AppendText(translator.Translate(context, node, JoinSection.Condition));
          node.Expression.AcceptVisitor(this);
        }
        context.Output.AppendText(translator.Translate(context, node, JoinSection.Exit));
      }
    }

    public virtual void Visit(SqlJoinHint node)
    {
      // nothing
    }

    public virtual void Visit(SqlLike node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, LikeSection.Entry));
        node.Expression.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, LikeSection.Like));
        node.Pattern.AcceptVisitor(this);
        if (!node.Escape.IsNullReference()) {
          context.Output.AppendText(translator.Translate(context, node, LikeSection.Escape));
          node.Escape.AcceptVisitor(this);
        }
        context.Output.AppendText(translator.Translate(context, node, LikeSection.Exit));
      }
    }

    public virtual void Visit(SqlLiteral node)
    {
      context.Output.AppendText(translator.Translate(context, node.LiteralType, node.GetValue()));
    }

    public virtual void Visit(SqlMatch node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, MatchSection.Entry));
        node.Value.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, MatchSection.Specification));
        node.SubQuery.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, MatchSection.Exit));
      }
    }

    public virtual void Visit(SqlNull node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlNextValue node)
    {
      context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
      context.Output.AppendText(translator.Translate(node.Sequence));
      context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
    }

    public virtual void Visit(SqlOpenCursor node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlOrder node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        if (!node.Expression.IsNullReference())
          node.Expression.AcceptVisitor(this);
        else if (node.Position > 0)
          context.Output.AppendText(node.Position.ToString());
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlParameterRef node)
    {
      var name = string.IsNullOrEmpty(node.Name)
        ? context.ParameterNameProvider.GetName(node.Parameter)
        : node.Name;
      context.Output.AppendText(translator.ParameterPrefix + name);
    }

    public virtual void Visit(SqlQueryRef node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, TableSection.Entry));
        node.Query.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, TableSection.Exit));
        context.Output.AppendText(translator.Translate(context, node, TableSection.AliasDeclaration));
      }
    }

    public virtual void Visit(SqlQueryExpression node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, QueryExpressionSection.Entry));
        context.Output.AppendText("(");
        node.Left.AcceptVisitor(this);
        context.Output.AppendText(")");
        context.Output.AppendText(translator.Translate(node.NodeType));
        context.Output.AppendText(translator.Translate(context, node, QueryExpressionSection.All));
        context.Output.AppendText("(");
        node.Right.AcceptVisitor(this);
        context.Output.AppendText(")");
        context.Output.AppendText(translator.Translate(context, node, QueryExpressionSection.Exit));
      }
    }

    public virtual void Visit(SqlRow node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        using (context.EnterCollectionScope()) {
          foreach (SqlExpression item in node) {
            if (!context.IsEmpty)
              context.Output.AppendDelimiter(translator.RowItemDelimiter);
            item.AcceptVisitor(this);
          }
        }
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlRowNumber node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        using (context.EnterCollectionScope()) {
          foreach (var item in node.OrderBy) {
            if (!context.IsEmpty)
              context.Output.AppendDelimiter(translator.ColumnDelimiter);
            item.AcceptVisitor(this);
          }
        }
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlRenameTable node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlRound node)
    {
      SqlExpression result;
      switch (node.Mode) {
      case MidpointRounding.ToEven:
        result = node.Length.IsNullReference()
          ? SqlHelper.BankersRound(node.Argument)
          : SqlHelper.BankersRound(node.Argument, node.Length);
        break;
      case MidpointRounding.AwayFromZero:
        result = node.Length.IsNullReference()
          ? SqlHelper.RegularRound(node.Argument)
          : SqlHelper.RegularRound(node.Argument, node.Length);
        break;
      default:
        throw new ArgumentOutOfRangeException();
      }
      if (node.Type==TypeCode.Decimal)
        result = SqlDml.Cast(result, decimalType);
      result.AcceptVisitor(this);
    }

    public virtual void Visit(SqlSelect node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, SelectSection.Entry));
        VisitSelectHints(node);
        VisitSelectColumns(node);
        VisitSelectFrom(node);
        VisitSelectWhere(node);
        VisitSelectGroupBy(node);
        VisitSelectOrderBy(node);
        VisitSelectLimitOffset(node);
        VisitSelectLock(node);
        context.Output.AppendText(translator.Translate(context, node, SelectSection.Exit));
      }
    }

    public virtual void VisitSelectHints(SqlSelect node)
    {
      if (node.Hints.Count==0)
        return;
      using (context.EnterCollectionScope()) {
        context.Output.AppendText(translator.Translate(context, node, SelectSection.HintsEntry));
        node.Hints[0].AcceptVisitor(this);
        for (int i = 1; i < node.Hints.Count; i++) {
          context.Output.AppendDelimiter(translator.HintDelimiter);
          node.Hints[i].AcceptVisitor(this);
        }
        context.Output.AppendText(translator.Translate(context, node, SelectSection.HintsExit));
      }
    }

    public virtual void VisitSelectColumns(SqlSelect node)
    {
      if (node.Columns.Count <= 0)
        return;
      using (context.EnterCollectionScope()) {
        foreach (SqlColumn item in node.Columns) {
          if (item is SqlColumnStub)
            continue;
          var cr = item as SqlColumnRef;
          if (!cr.IsNullReference() && cr.SqlColumn is SqlColumnStub)
            continue;
            
          if (!context.IsEmpty)
            context.Output.AppendDelimiter(translator.ColumnDelimiter);
          if (!cr.IsNullReference()) {
            cr.SqlColumn.AcceptVisitor(this);
            context.Output.AppendText(translator.Translate(context, cr, ColumnSection.AliasDeclaration));
          }
          else
            item.AcceptVisitor(this);
        }
      }
    }

    public virtual void VisitSelectFrom(SqlSelect node)
    {
      if (node.From==null)
        return;
      context.Output.AppendText(translator.Translate(context, node, SelectSection.From));
      node.From.AcceptVisitor(this);
    }

    public virtual void VisitSelectWhere(SqlSelect node)
    {
      if (node.Where.IsNullReference())
        return;
      context.Output.AppendText(translator.Translate(context, node, SelectSection.Where));
      node.Where.AcceptVisitor(this);
    }

    public virtual void VisitSelectGroupBy(SqlSelect node)
    {
      if (node.GroupBy.Count <= 0)
        return;
      // group by
      context.Output.AppendText(translator.Translate(context, node, SelectSection.GroupBy));
      using (context.EnterCollectionScope()) {
        foreach (SqlColumn item in node.GroupBy) {
          if (!context.IsEmpty)
            context.Output.AppendDelimiter(translator.ColumnDelimiter);
          var cr = item as SqlColumnRef;
          if (!cr.IsNullReference())
            cr.SqlColumn.AcceptVisitor(this);
          else
            item.AcceptVisitor(this);
        }
      }
      if (node.Having.IsNullReference())
        return;
      // having
      context.Output.AppendText(translator.Translate(context, node, SelectSection.Having));
      node.Having.AcceptVisitor(this);
    }

    public virtual void VisitSelectOrderBy(SqlSelect node)
    {
      if (node.OrderBy.Count <= 0)
        return;
      context.Output.AppendText(translator.Translate(context, node, SelectSection.OrderBy));
      using (context.EnterCollectionScope()) {
        foreach (SqlOrder item in node.OrderBy) {
          if (!context.IsEmpty)
            context.Output.AppendDelimiter(translator.ColumnDelimiter);
          item.AcceptVisitor(this);
        }
      }
    }

    public virtual void VisitSelectLimitOffset(SqlSelect node)
    {
      if (!node.Limit.IsNullReference()) {
        context.Output.AppendText(translator.Translate(context, node, SelectSection.Limit));
        node.Limit.AcceptVisitor(this);
      }
      if (!node.Offset.IsNullReference()) {
        context.Output.AppendText(translator.Translate(context, node, SelectSection.Offset));
        node.Offset.AcceptVisitor(this);
      }
    }

    public virtual void VisitSelectLock(SqlSelect node)
    {
      if (node.Lock!=SqlLockType.Empty)
        context.Output.AppendText(translator.Translate(node.Lock));
    }

    public virtual void Visit(SqlStatementBlock node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        using (context.EnterCollectionScope()) {
          foreach (SqlStatement item in node) {
            item.AcceptVisitor(this);
            context.Output.AppendDelimiter(translator.BatchItemDelimiter, SqlDelimiterType.Column);
          }
        }
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlSubQuery node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Query.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlTableColumn node)
    {
      context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
    }

    public virtual void Visit(SqlTableRef node)
    {
      context.Output.AppendText(
        translator.Translate(context, node, TableSection.Entry)+
        translator.Translate(context, node, TableSection.AliasDeclaration));
    }

    public virtual void Visit(SqlTrim node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, TrimSection.Entry));
        context.Output.AppendText(translator.Translate(node.TrimType));
        if (node.TrimCharacters!=null)
          context.Output.AppendText(translator.Translate(context, typeof(string), node.TrimCharacters));
        context.Output.AppendText(translator.Translate(context, node, TrimSection.From));
        node.Expression.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, TrimSection.Exit));
      }
    }

    public virtual void Visit(SqlUnary node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Operand.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlUpdate node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, UpdateSection.Entry));

        if (node.Update==null)
          throw new SqlCompilerException(Strings.ExTablePropertyIsNotSet);

        using (context.EnterScope(context.NamingOptions & ~SqlCompilerNamingOptions.TableAliasing)) {
          node.Update.AcceptVisitor(this);
        }

        context.Output.AppendText(translator.Translate(context, node, UpdateSection.Set));

        using (context.EnterCollectionScope()) {
          foreach (ISqlLValue item in node.Values.Keys) {
            if (!context.IsEmpty)
              context.Output.AppendDelimiter(translator.ColumnDelimiter);
            var tc = item as SqlTableColumn;
            if (!tc.IsNullReference() && tc.SqlTable!=node.Update)
              throw new SqlCompilerException(string.Format(Strings.ExUnboundColumn, tc.Name));
            context.Output.AppendText(translator.QuoteIdentifier(tc.Name));
            context.Output.AppendText(translator.Translate(SqlNodeType.Equals));
            SqlExpression value = node.Values[item];
            value.AcceptVisitor(this);
          }
        }

        if (node.From!=null) {
          context.Output.AppendText(translator.Translate(context, node, UpdateSection.From));
          node.From.AcceptVisitor(this);
        }

        if (!node.Where.IsNullReference()) {
            context.Output.AppendText(translator.Translate(context, node, UpdateSection.Where));
            node.Where.AcceptVisitor(this);
        }

        context.Output.AppendText(translator.Translate(context, node, UpdateSection.Exit));
      }
    }

    public virtual void Visit(SqlPlaceholder node)
    {
      context.Output.AppendPlaceholder(node.Id);
    }

    public virtual void Visit(SqlUserColumn node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Expression.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlUserFunctionCall node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, FunctionCallSection.Entry, -1));
        if (node.Arguments.Count>0) {
          using (context.EnterCollectionScope()) {
            int argumentPosition = 0;
            foreach (SqlExpression item in node.Arguments) {
              if (!context.IsEmpty)
                context.Output.AppendDelimiter(translator.Translate(context, node, FunctionCallSection.ArgumentDelimiter, argumentPosition++));
              item.AcceptVisitor(this);
            }
          }
        }
        context.Output.AppendText(translator.Translate(context, node, FunctionCallSection.Exit, -1));
      }
    }

    public virtual void Visit(SqlVariable node)
    {
      context.Output.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlVariant node)
    {
      using (context.EnterMainVariantScope(node.Id))
        node.Main.AcceptVisitor(this);

      using (context.EnterAlternativeVariantScope(node.Id))
        node.Alternative.AcceptVisitor(this);
    }

    public virtual void Visit(SqlWhile node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, WhileSection.Entry));
        node.Condition.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, WhileSection.Statement));
        node.Statement.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, WhileSection.Exit));
      }
    }

    public virtual void Visit(TableColumn column)
    {
      context.Output.AppendText(translator.Translate(context, column, TableColumnSection.Entry));
      if (column.Expression.IsNullReference()) {
        if (column.Domain==null)
          ArgumentValidator.EnsureArgumentNotNull(column.DataType, "DataType");
        context.Output.AppendText(translator.Translate(context, column, TableColumnSection.Type));
      }
      if (column.Collation!=null) {
        context.Output.AppendText(translator.Translate(context, column, TableColumnSection.Collate));
      }
      if (!column.DefaultValue.IsNullReference()) {
        context.Output.AppendText(translator.Translate(context, column, TableColumnSection.DefaultValue));
        column.DefaultValue.AcceptVisitor(this);
      }
      else if (column.SequenceDescriptor!=null) {
        context.Output.AppendText(translator.Translate(context, column, TableColumnSection.GeneratedEntry));
        context.Output.AppendText(translator.Translate(context, column.SequenceDescriptor, SequenceDescriptorSection.StartValue));
        context.Output.AppendText(translator.Translate(context, column.SequenceDescriptor, SequenceDescriptorSection.Increment));
        context.Output.AppendText(translator.Translate(context, column.SequenceDescriptor, SequenceDescriptorSection.MaxValue));
        context.Output.AppendText(translator.Translate(context, column.SequenceDescriptor, SequenceDescriptorSection.MinValue));
        context.Output.AppendText(translator.Translate(context, column.SequenceDescriptor, SequenceDescriptorSection.IsCyclic));
        context.Output.AppendText(translator.Translate(context, column, TableColumnSection.GeneratedExit));
      }
      else if (!column.Expression.IsNullReference()) {
        context.Output.AppendText(translator.Translate(context, column, TableColumnSection.GenerationExpressionEntry));
        using (context.EnterScope(context.NamingOptions & ~SqlCompilerNamingOptions.TableQualifiedColumns)) {
          column.Expression.AcceptVisitor(this);
        }
        context.Output.AppendText(translator.Translate(context, column, TableColumnSection.GenerationExpressionExit));
      }
      if (!column.IsNullable)
        context.Output.AppendText(translator.Translate(context, column, TableColumnSection.NotNull));
      context.Output.AppendText(translator.Translate(context, column, TableColumnSection.Exit));
    }
    
    private void Visit(TableConstraint constraint)
    {
      context.Output.AppendText(translator.Translate(context, constraint, ConstraintSection.Entry));
      if (constraint is CheckConstraint) {
        context.Output.AppendText(translator.Translate(context, constraint, ConstraintSection.Check));
        ((CheckConstraint)constraint).Condition.AcceptVisitor(this);
      }
      else if (constraint is UniqueConstraint) {
        if (constraint is PrimaryKey)
          context.Output.AppendText(translator.Translate(context, constraint, ConstraintSection.PrimaryKey));
        else
          context.Output.AppendText(translator.Translate(context, constraint, ConstraintSection.Unique));
        if (((UniqueConstraint)constraint).Columns.Count>0)
          using (context.EnterCollectionScope()) {
            foreach (TableColumn tc in ((UniqueConstraint)constraint).Columns) {
              if (!context.IsEmpty)
                context.Output.AppendDelimiter(translator.ColumnDelimiter);
              context.Output.AppendText(
                translator.Translate(context, tc, TableColumnSection.Entry));
            }
          }
      }
      else if (constraint is ForeignKey) {
        context.Output.AppendText(translator.Translate(context, constraint, ConstraintSection.ForeignKey));
        ForeignKey fk = constraint as ForeignKey;
        if (fk.ReferencedColumns.Count==0)
          throw new SqlCompilerException(Strings.ExReferencedColumnsCountCantBeLessThenOne);
        if (fk.Columns.Count==0)
          throw new SqlCompilerException(Strings.ExReferencingColumnsCountCantBeLessThenOne);
        using (context.EnterCollectionScope()) {
          foreach (TableColumn tc in fk.Columns) {
            if (!context.IsEmpty)
              context.Output.AppendDelimiter(translator.ColumnDelimiter);
            context.Output.AppendText(translator.Translate(context, tc, TableColumnSection.Entry));
          }
        }
        context.Output.AppendText(translator.Translate(context, constraint, ConstraintSection.ReferencedColumns));
        using (context.EnterCollectionScope()) {
          foreach (TableColumn tc in fk.ReferencedColumns) {
            if (!context.IsEmpty)
              context.Output.AppendDelimiter(translator.ColumnDelimiter);
            context.Output.AppendText(
              translator.Translate(context, tc, TableColumnSection.Entry));
          }
        }
      }
      context.Output.AppendText(translator.Translate(context, constraint, ConstraintSection.Exit));
    }

    public virtual void Visit(SqlExtract node)
    {
      using (context.EnterScope(node)) {
        context.Output.AppendText(translator.Translate(context, node, ExtractSection.Entry));
        var part = node.DateTimePart!=SqlDateTimePart.Nothing
          ? translator.Translate(node.DateTimePart)
          : translator.Translate(node.IntervalPart);
        context.Output.AppendText(part);
        context.Output.AppendText(translator.Translate(context, node, ExtractSection.From));
        node.Operand.AcceptVisitor(this);
        context.Output.AppendText(translator.Translate(context, node, ExtractSection.Exit));
      }
    }

    private void TranslateDynamicFilterViaInOperator(SqlDynamicFilter node)
    {
      int numberOfExpressions = node.Expressions.Count;
      bool isMulticolumn = numberOfExpressions > 1;
      string delimiter = translator.RowItemDelimiter + " ";
      SqlExpression filteredExpression = isMulticolumn ? SqlDml.Row(node.Expressions) : node.Expressions[0];
      filteredExpression.AcceptVisitor(this);
      context.Output.AppendText(translator.Translate(SqlNodeType.In));
      context.Output.AppendText(translator.RowBegin);
      using (context.EnterCycleBodyScope(node.Id, delimiter)) {
        if (isMulticolumn) {
          context.Output.AppendText(translator.RowBegin);
          for (int i = 0; i < numberOfExpressions; i++) {
            context.Output.AppendCycleItem(i);
            if (i!=numberOfExpressions - 1)
              context.Output.AppendDelimiter(translator.RowItemDelimiter);
          }
          context.Output.AppendText(translator.RowEnd);
        }
        else
          context.Output.AppendCycleItem(0);
      }
      using (context.EnterCycleEmptyCaseScope(node.Id)) {
        SqlExpression nullExpression = isMulticolumn
          ? SqlDml.Row(Enumerable.Repeat(SqlDml.Null, numberOfExpressions).ToArray())
          : (SqlExpression) SqlDml.Null;
        nullExpression.AcceptVisitor(this);
      }
      context.Output.AppendText(translator.RowEnd);
    }

    private void TranslateDynamicFilterViaMultipleComparisons(SqlDynamicFilter node)
    {
      int numberOfExpressions = node.Expressions.Count;
      string delimiter = " " + translator.Translate(SqlNodeType.Or) + " ";
      using (context.EnterCycleBodyScope(node.Id, delimiter)) {
        context.Output.AppendText(translator.OpeningParenthesis);
        for (int i = 0; i < numberOfExpressions; i++) {
          node.Expressions[i].AcceptVisitor(this);
          context.Output.AppendText(translator.Translate(SqlNodeType.Equals));
          context.Output.AppendCycleItem(i);
          if (i!=numberOfExpressions - 1)
            context.Output.AppendText(translator.Translate(SqlNodeType.And));
        }
        context.Output.AppendText(translator.ClosingParenthesis);
      }
      using (context.EnterCycleEmptyCaseScope(node.Id)) {
        SqlDml.Literal(false).AcceptVisitor(this);
      }
    }

    private static IEnumerable<SqlStatement> FlattenBatch(SqlBatch batch)
    {
      foreach (SqlStatement statement in batch) {
        var nestedBatch = statement as SqlBatch;
        if (nestedBatch!=null) {
          foreach (var nestedStatement in FlattenBatch(nestedBatch))
            yield return nestedStatement;
        }
        else {
          yield return statement;
        }
      }
    }


    // Constructors

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    /// <param name="driver">The driver.</param>
    protected SqlCompiler(SqlDriver driver)
    {
      ArgumentValidator.EnsureArgumentNotNull(driver, "driver");
      translator = driver.Translator;
      decimalType = driver.TypeMappings.Decimal.BuildSqlType();
      this.driver = driver;
    }
  }
}
