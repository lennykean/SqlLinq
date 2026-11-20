using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Dynamic;
using System.Reflection;

namespace SqlLinq
{
    class DynamicGetMemberBinder : GetMemberBinder
    {
        private readonly static PropertyInfo _indexer = typeof(IDictionary<string, object>).GetProperty("Item");

        public DynamicGetMemberBinder(string name)
            : base(name, true)
        {

        }

        public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
        {
            if (target.Value == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "{0} can't be read because the row is null.", Name));
            }

            var restrictions = BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType);
            if (target.Value is IDictionary<string, object>)
            {
                return new DynamicMetaObject(Expression.MakeIndex(Expression.Convert(target.Expression, typeof(IDictionary<string, object>)), _indexer, new Expression[] { Expression.Constant(this.Name) }), restrictions);
            }

            var member = target.LimitType.GetPropertyOrField(Name) ?? throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} can't be read because it isn't a field of {1}.", Name, target.LimitType.Name), SqlErrorKind.UnknownName);

            return new DynamicMetaObject(Expression.Convert(Expression.MakeMemberAccess(Expression.Convert(target.Expression, member.DeclaringType), member), typeof(object)), restrictions);
        }
    }
}
