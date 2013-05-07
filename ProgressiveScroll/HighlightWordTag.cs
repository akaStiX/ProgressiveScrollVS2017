using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace ProgressiveScroll
{
	public static class TypeExports
	{
		[Export(typeof (ClassificationTypeDefinition))]
		[Name(ClassificationNames.Highlights)]
		public static ClassificationTypeDefinition HighlightWordClassificationType;
	}

	internal class HighlightWordTag : ClassificationTag
	{
		public HighlightWordTag(IClassificationType type) : base(type) {}
	}
}