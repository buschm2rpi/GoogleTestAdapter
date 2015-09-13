﻿using Moq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System.Linq;
using System.Collections.Generic;

namespace GoogleTestAdapter
{
    public abstract class AbstractGoogleTestExecutorTests : AbstractGoogleTestExtensionTests
    {

        protected abstract bool ParallelTestExecution { get; }
        protected abstract int MaxNrOfThreads { get; }

        protected virtual void CheckMockInvocations(int nrOfPassedTests, int nrOfFailedTests, int nrOfUnexecutedTests, int nrOfNotFoundTests, Mock<IFrameworkHandle> MockHandle)
        {
            MockHandle.Verify(h => h.RecordResult(It.Is<TestResult>(tr => tr.Outcome == TestOutcome.None)),
                Times.Exactly(nrOfUnexecutedTests));
            MockHandle.Verify(h => h.RecordEnd(It.IsAny<TestCase>(), It.Is<TestOutcome>(TO => TO == TestOutcome.None)),
                Times.Exactly(nrOfUnexecutedTests));
        }

        class CollectingTestDiscoverySink : ITestCaseDiscoverySink
        {
            public List<TestCase> TestCases = new List<TestCase>();

            public void SendTestCase(TestCase discoveredTest)
            {
                TestCases.Add(discoveredTest);
            }
        }

        [TestInitialize]
        override public void SetUp()
        {
            base.SetUp();

            MockOptions.Setup(O => O.ParallelTestExecution).Returns(ParallelTestExecution);
            MockOptions.Setup(O => O.MaxNrOfThreads).Returns(MaxNrOfThreads);
        }

        [TestMethod]
        public void CheckThatTestDirectoryIsPassedViaCommandLineArg()
        {
            Mock<IFrameworkHandle> MockHandle = new Mock<IFrameworkHandle>();
            Mock<IRunContext> MockRunContext = new Mock<IRunContext>();
            Mock<IDiscoveryContext> MockDiscoveryContext = new Mock<IDiscoveryContext>();
            CollectingTestDiscoverySink sink = new CollectingTestDiscoverySink();

            GoogleTestDiscoverer discoverer = new GoogleTestDiscoverer(MockOptions.Object);
            discoverer.DiscoverTests(GoogleTestDiscovererTests.x86traitsTests.Yield(), MockDiscoveryContext.Object, MockHandle.Object, sink);

            TestCase testcase = sink.TestCases.Where(TC => TC.FullyQualifiedName.Contains("CommandArgs.TestDirectoryIsSet")).FirstOrDefault();
            Assert.IsNotNull(testcase);

            GoogleTestExecutor Executor = new GoogleTestExecutor(MockOptions.Object);
            Executor.RunTests(testcase.Yield(), MockRunContext.Object, MockHandle.Object);

            MockHandle.Verify(h => h.RecordEnd(It.IsAny<TestCase>(), It.Is<TestOutcome>(TO => TO == TestOutcome.Passed)),
                Times.Exactly(0));
            MockHandle.Verify(h => h.RecordEnd(It.IsAny<TestCase>(), It.Is<TestOutcome>(TO => TO == TestOutcome.Failed)),
                Times.Exactly(1));

            MockHandle.Reset();
            MockOptions.Setup(O => O.AdditionalTestExecutionParam).Returns("-testdirectory=\"${TestDirectory}\"");

            Executor = new GoogleTestExecutor(MockOptions.Object);
            Executor.RunTests(testcase.Yield(), MockRunContext.Object, MockHandle.Object);

            MockHandle.Verify(h => h.RecordEnd(It.IsAny<TestCase>(), It.Is<TestOutcome>(TO => TO == TestOutcome.Passed)),
                Times.Exactly(1));
            MockHandle.Verify(h => h.RecordEnd(It.IsAny<TestCase>(), It.Is<TestOutcome>(TO => TO == TestOutcome.Failed)),
                Times.Exactly(0));
        }

        [TestMethod]
        public void RunsExternallyLinkedX86TestsWithResult()
        {
            RunAndVerifyTests(GoogleTestDiscovererTests.x86externallyLinkedTests, 2, 0, 0);
        }

        [TestMethod]
        public void RunsStaticallyLinkedX86TestsWithResult()
        {
            RunAndVerifyTests(GoogleTestDiscovererTests.x86staticallyLinkedTests, 1, 1, 0);
        }

        [TestMethod]
        public void RunsExternallyLinkedX64TestsWithResult()
        {
            RunAndVerifyTests(GoogleTestDiscovererTests.x64externallyLinkedTests, 2, 0, 0);
        }

        [TestMethod]
        public void RunsStaticallyLinkedX64TestsWithResult()
        {
            RunAndVerifyTests(GoogleTestDiscovererTests.x64staticallyLinkedTests, 1, 1, 0);
        }

        [TestMethod]
        public void RunsCrashingX64TestsWithoutResult()
        {
            RunAndVerifyTests(GoogleTestDiscovererTests.x64crashingTests, 0, 1, 0, 1);
        }

        [TestMethod]
        public void RunsCrashingX86TestsWithoutResult()
        {
            RunAndVerifyTests(GoogleTestDiscovererTests.x86crashingTests, 0, 1, 0, 1);
        }

        [TestMethod]
        public void RunsHardCrashingX86TestsWithoutResult()
        {
            Mock<IFrameworkHandle> MockHandle = new Mock<IFrameworkHandle>();
            Mock<IRunContext> MockRunContext = new Mock<IRunContext>();

            GoogleTestExecutor Executor = new GoogleTestExecutor(MockOptions.Object);
            Executor.RunTests(GoogleTestDiscovererTests.x86hardcrashingTests.Yield(), MockRunContext.Object, MockHandle.Object);

            MockHandle.Verify(h => h.RecordResult(It.Is<TestResult>(tr => tr.Outcome == TestOutcome.Passed)),
                Times.Exactly(0));
            MockHandle.Verify(h => h.RecordResult(It.Is<TestResult>(tr => tr.Outcome == TestOutcome.Failed && tr.ErrorMessage == "!! This is probably the test that crashed !!")),
                Times.Exactly(1));
            MockHandle.Verify(h => h.RecordResult(It.Is<TestResult>(tr => tr.Outcome == TestOutcome.None)),
                Times.Exactly(0));
            MockHandle.Verify(h => h.RecordResult(It.Is<TestResult>(tr => tr.Outcome == TestOutcome.Skipped && tr.ErrorMessage == "reason is probably a crash of test Crashing.TheCrash")),
                Times.Exactly(2));

            MockHandle.Verify(h => h.RecordEnd(It.IsAny<TestCase>(), It.Is<TestOutcome>(TO => TO == TestOutcome.Passed)),
                Times.Exactly(0));
            MockHandle.Verify(h => h.RecordEnd(It.IsAny<TestCase>(), It.Is<TestOutcome>(TO => TO == TestOutcome.Failed)),
                Times.Exactly(1));
            MockHandle.Verify(h => h.RecordEnd(It.IsAny<TestCase>(), It.Is<TestOutcome>(TO => TO == TestOutcome.None)),
                Times.Exactly(0));
            MockHandle.Verify(h => h.RecordEnd(It.IsAny<TestCase>(), It.Is<TestOutcome>(TO => TO == TestOutcome.Skipped)),
                Times.Exactly(2));
        }

        private void RunAndVerifyTests(string executable, int nrOfPassedTests, int nrOfFailedTests, int nrOfUnexecutedTests, int nrOfNotFoundTests = 0)
        {
            Mock<IFrameworkHandle> MockHandle = new Mock<IFrameworkHandle>();
            Mock<IRunContext> MockRunContext = new Mock<IRunContext>();

            GoogleTestExecutor Executor = new GoogleTestExecutor(MockOptions.Object);
            Executor.RunTests(executable.Yield(), MockRunContext.Object, MockHandle.Object);

            CheckMockInvocations(nrOfPassedTests, nrOfFailedTests, nrOfUnexecutedTests, nrOfNotFoundTests, MockHandle);
        }

    }

}