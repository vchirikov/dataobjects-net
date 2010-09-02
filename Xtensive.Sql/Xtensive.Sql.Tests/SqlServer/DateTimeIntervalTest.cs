// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Denis Krjuchkov
// Created:    2009.03.02

using NUnit.Framework;

namespace Xtensive.Sql.Tests.SqlServer
{
  [TestFixture]
  public class DateTimeIntervalTest : Tests.DateTimeIntervalTest
  {
    protected override string Url { get { return TestUrl.SqlServer2005; } }

    public override void DateTimeSubtractIntervalTest()
    {
      Assert.Ignore("MSSQL DateTime precision issue");
    }
  }
}