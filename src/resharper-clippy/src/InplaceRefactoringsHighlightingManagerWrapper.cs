﻿using System;
using System.Linq.Expressions;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.InplaceRefactorings;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.ReSharper.Refactorings.Workflow;

namespace CitizenMatt.ReSharper.Plugins.Clippy
{
    public enum InplaceRefactoringType
    {
        None,
        Rename,
        ChangeSignature,
        MoveStaticMembers,
        CopyPaste
    }

    [SolutionComponent]
    public class InplaceRefactoringsHighlightingManagerWrapper
    {
        private static readonly Func<ISolution, object> GetInstance;
        private static readonly Func<object, IPsiSourceFile, int, object> GetRefactoringAvailable;

        private readonly ISolution solution;
        private readonly object manager;

        static InplaceRefactoringsHighlightingManagerWrapper()
        {
            var managerType = typeof(InplaceRefactoringsServices).Assembly.GetType(
                "JetBrains.ReSharper.InplaceRefactorings.InplaceRefactoringsManager");
            GetInstance = BuildGetInstanceFunc(managerType);
            GetRefactoringAvailable = BuildGetRefactoringAvailable(managerType);
        }

        public InplaceRefactoringsHighlightingManagerWrapper(ISolution solution)
        {
            this.solution = solution;
            if (GetInstance != null)
                manager = GetInstance(solution);
        }

        public InplaceRefactoringInfo GetInplaceRefactoringType(IDocument document, int caretOffset)
        {
            if (GetRefactoringAvailable == null)
                return InplaceRefactoringInfo.None;

            using(CommitCookie.Commit(solution))
            {
                Func<object> callGetRefactoringAvailable = () => GetRefactoringAvailable(manager, document.GetPsiSourceFile(solution), caretOffset);
                var refactoringInfo = callGetRefactoringAvailable();
                if (refactoringInfo == null)
                    return InplaceRefactoringInfo.None;

                switch (refactoringInfo.GetType().Name)
                {
                    case "RenameInfo":
                        return new InplaceRefactoringInfo(callGetRefactoringAvailable, InplaceRefactoringType.Rename);

                    case "ChangeSignatureInfo":
                        return new InplaceRefactoringInfo(callGetRefactoringAvailable, InplaceRefactoringType.ChangeSignature);

                    case "MoveStaticMembersInfo":
                        return new InplaceRefactoringInfo(callGetRefactoringAvailable, InplaceRefactoringType.MoveStaticMembers);
                }
            }

            return InplaceRefactoringInfo.None;
        }

        private static Func<ISolution, object> BuildGetInstanceFunc(Type managerType)
        {
            if (managerType != null)
            {
                var solutionParameter = Expression.Parameter(typeof (ISolution), "solution");
                var getInstanceCall = Expression.Call(managerType, "GetInstance", null, solutionParameter);
                return Expression.Lambda<Func<ISolution, object>>(getInstanceCall, solutionParameter).Compile();
            }

            return null;
        }

        private static Func<object, IPsiSourceFile, int, object> BuildGetRefactoringAvailable(Type managerType)
        {
            if (managerType != null)
            {
                var managerParameter = Expression.Parameter(typeof(object), "manager");
                var managerInstance = Expression.Convert(managerParameter, managerType);
                var fileParameter = Expression.Parameter(typeof (IPsiSourceFile), "sourceFile");
                var offsetParameter = Expression.Parameter(typeof (int), "caretOffset");
                var getRefactoringAvailableCall = Expression.Call(managerInstance, "GetRefactoringAvailable", null, fileParameter, offsetParameter);
                return Expression.Lambda<Func<object, IPsiSourceFile, int, object>>(getRefactoringAvailableCall, managerParameter, fileParameter, offsetParameter).Compile();
            }

            return null;
        }
    }

    public class InplaceRefactoringInfo
    {
        public static readonly InplaceRefactoringInfo None = new InplaceRefactoringInfo(null, 
            InplaceRefactoringType.None);

        private static readonly Func<object, IRefactoringWorkflow> CreateRefactoringWorkflowImpl;

        private readonly Func<object> getRefactoringInfo;

        static InplaceRefactoringInfo()
        {
            var refactoringInfoType = typeof(InplaceRefactoringsServices).Assembly.GetType(
                "JetBrains.ReSharper.InplaceRefactorings.IRefactoringInfo");

            var refactoringInfoParameter = Expression.Parameter(typeof (object), "refactoringInfo");
            var refactoringInfoInstance = Expression.Convert(refactoringInfoParameter, refactoringInfoType);
            var callExpression = Expression.Call(refactoringInfoInstance, "CreateRefactoringWorkflow", null);
            CreateRefactoringWorkflowImpl = Expression.Lambda<Func<object, IRefactoringWorkflow>>(callExpression, refactoringInfoParameter).Compile();
        }

        public InplaceRefactoringInfo(Func<object> getRefactoringInfo,
            InplaceRefactoringType inplaceRefactoringType)
        {
            this.getRefactoringInfo = getRefactoringInfo;
            Type = inplaceRefactoringType;
        }

        public InplaceRefactoringType Type { get; private set; }

        public IRefactoringWorkflow CreateRefactoringWorkflow()
        {
            if (getRefactoringInfo != null && CreateRefactoringWorkflowImpl != null)
            {
                var refactoringInfo = getRefactoringInfo();
                if (refactoringInfo != null)
                    return CreateRefactoringWorkflowImpl(refactoringInfo);
            }

            return null;
        }
    }
}