using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class ChapterInitializationFailureTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        [Test] public void NestedInitializationKeepsYieldOrderAndDisposesEveryScope()
        {
            var order=new List<string>();var first=new object();var second=new object();
            IEnumerator Child(){try{order.Add("child");yield return second;}finally{order.Add("child disposed");}}
            IEnumerator Parent(){try{yield return first;yield return Child();order.Add("ready");}finally{order.Add("parent disposed");}}
            var guard=ChapterSceneFade.GuardInitialization(Parent());Assert.That(guard.MoveNext(),Is.True);Assert.That(guard.Current,Is.SameAs(first));
            Assert.That(guard.MoveNext(),Is.True);Assert.That(guard.Current,Is.SameAs(second));Assert.That(guard.MoveNext(),Is.False);
            Assert.That(order,Is.EqualTo(new[]{"child","child disposed","ready","parent disposed"}));
        }
        [Test] public void NestedExceptionOpensFailureAndReleasesTheInterruptedCompositionScope()
        {
            var go=new GameObject("Inactive loading controller");go.SetActive(false);bool released=false;
            try
            {
                var fade=go.AddComponent<ChapterSceneFade>();typeof(ChapterSceneFade).GetField("active",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,fade);
                IEnumerator Child(){yield return null;throw new InvalidOperationException("fixture chapter preparation failed");}
                IEnumerator Parent(){try{yield return Child();Assert.Fail("Failed initialization must not become ready");}finally{released=true;}}
                LogAssert.Expect(LogType.Warning,"Scene loading failed: fixture chapter preparation failed");
                LogAssert.Expect(LogType.Exception,new Regex("InvalidOperationException: fixture chapter preparation failed"));
                var guard=ChapterSceneFade.GuardInitialization(Parent());Assert.That(guard.MoveNext(),Is.True);Assert.That(guard.MoveNext(),Is.False);
                Assert.That(ChapterSceneFade.ShowingFailure,Is.True);Assert.That(released,Is.True);
                Assert.That(typeof(ChapterSceneFade).GetField("initializationError",Hidden).GetValue(fade),Is.TypeOf<InvalidOperationException>());
            }
            finally{Object.DestroyImmediate(go);}
        }
        [Test] public void FailedPreparationOffersATitleReturnInsteadOfRetryingAnIncompleteWorld()
        {
            var go=new GameObject("Inactive failure view",typeof(RectTransform));go.SetActive(false);
            try
            {
                var fade=go.AddComponent<ChapterSceneFade>();var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                typeof(ChapterSceneFade).GetMethod("BuildFailure",Hidden).Invoke(fade,new object[]{(RectTransform)go.transform,font});
                typeof(ChapterSceneFade).GetField("initializationError",Hidden).SetValue(fade,new InvalidOperationException("fixture preparation"));
                LogAssert.Expect(LogType.Warning,"Scene loading failed: fixture preparation");
                typeof(ChapterSceneFade).GetMethod("ShowFailure",Hidden).Invoke(fade,new object[]{new InvalidOperationException("fixture preparation")});
                var content=go.transform.Find("Loading failure/Failure content");
                Assert.That(content.Find("다시 시도/Label").GetComponent<Text>().text,Is.EqualTo("타이틀로"));
                Assert.That(content.Find("다시 시도/Keycap/Key").GetComponent<Text>().text,Is.EqualTo("F"));
                Assert.That(content.Find("타이틀로").gameObject.activeSelf,Is.False);
                Assert.That(content.Find("Failure description").GetComponent<Text>().text,Does.Contain("타이틀로 돌아가"));
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
