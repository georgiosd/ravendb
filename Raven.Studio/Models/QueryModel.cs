using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using Raven.Abstractions.Data;
using Raven.Client.Linq;
using Raven.Studio.Commands;
using Raven.Studio.Features.Documents;
using Raven.Studio.Features.Query;
using Raven.Studio.Infrastructure;
using Raven.Studio.Extensions;
using Raven.Abstractions.Extensions;

namespace Raven.Studio.Models
{
	public class QueryModel : PageViewModel, IHasPageTitle
	{
        private ICommand executeQuery;
        private RavenQueryStatistics results;
        private bool skipTransformResults;
        private bool hasTransform;
        public QueryDocumentsCollectionSource CollectionSource { get; private set; }

		private QueryIndexAutoComplete queryIndexAutoComplete;
		public QueryIndexAutoComplete QueryIndexAutoComplete
		{
			get { return queryIndexAutoComplete; }
			set
			{
				queryIndexAutoComplete = value;
				OnPropertyChanged(() => QueryIndexAutoComplete);
			}
		}

		#region SpatialQuery

		private bool isSpatialQuerySupported;
		public bool IsSpatialQuerySupported
		{
			get { return isSpatialQuerySupported; }
			set
			{
				isSpatialQuerySupported = value;
				OnPropertyChanged(() => IsSpatialQuerySupported);
			}
		}

		private bool isSpatialQuery;
		public bool IsSpatialQuery
		{
			get { return isSpatialQuery; }
			set
			{
				isSpatialQuery = value;
				OnPropertyChanged(() => IsSpatialQuery);
			}
		}

		private double? latitude;
		public double? Latitude
		{
			get { return latitude; }
			set
			{
				latitude = value;
				OnPropertyChanged(() => Latitude);
			}
		}

		private double? longitude;
		public double? Longitude
		{
			get { return longitude; }
			set
			{
				longitude = value;
				OnPropertyChanged(() => Longitude);
			}
		}

		private double? radius;
		public double? Radius
		{
			get { return radius; }
			set
			{
				radius = value;
				OnPropertyChanged(() => Radius);
			}
		}

		#endregion

		private string indexName;
		public string IndexName
		{
			get
			{
				return indexName;
			}
			private set
			{
				if (string.IsNullOrWhiteSpace(value))
				{
					UrlUtil.Navigate("/indexes");
				}

				indexName = value;
                DocumentsResult.Context = "Index/" + indexName;
				OnPropertyChanged(() => IndexName);
			}
		}

	    private bool showFields;

	    public bool ShowFields
	    {
	        get { return showFields; }
            set
            {
                showFields = value;
                if (!SkipTransformResults)
                {
                    SkipTransformResults = true;
                }
                else
                {
                    // no need to do a requery if we've set SkipTransformResults, because that forces one too.
                    Requery();
                }
                OnPropertyChanged(() => ShowFields);
            }
	    }

	    public bool SkipTransformResults
	    {
	        get { return skipTransformResults; }
            set
            {
                skipTransformResults = value;
                OnPropertyChanged(() => SkipTransformResults);
                Requery();
            }
	    }
	    #region Sorting

		public const string SortByDescSuffix = " DESC";

		public class StringRef : NotifyPropertyChangedBase
		{
			private string value;
			public string Value
			{
				get { return value; }
				set { this.value = value; OnPropertyChanged(() => Value);}
			}
		}

		public BindableCollection<StringRef> SortBy { get; private set; }
		public BindableCollection<string> SortByOptions { get; private set; }

		public ICommand AddSortBy
		{
			get { return new ChangeFieldValueCommand<QueryModel>(this, x => x.SortBy.Add(new StringRef { Value = "" })); }
		}

		public ICommand RemoveSortBy
		{
			get { return new RemoveSortByCommand(this); }
		}

		private class RemoveSortByCommand : Command
		{
			private string field;
			private readonly QueryModel model;

			public RemoveSortByCommand(QueryModel model)
			{
				this.model = model;
			}

			public override bool CanExecute(object parameter)
			{
				field = parameter as string;
				return field != null && model.SortBy.Any(x => x.Value == field);
			}

			public override void Execute(object parameter)
			{
				if (CanExecute(parameter) == false)
					return;
				StringRef firstOrDefault = model.SortBy.FirstOrDefault(x => x.Value == field);
				if (firstOrDefault != null)
					model.SortBy.Remove(firstOrDefault);
			}
		}

		private void SetSortByOptions(ICollection<string> items)
		{
            SortByOptions.Clear();

			foreach (var item in items)
			{
				SortByOptions.Add(item);
				SortByOptions.Add(item + SortByDescSuffix);
			}
		}
		
		#endregion

		private bool isDynamicQuery;
		public bool IsDynamicQuery
		{
			get { return isDynamicQuery; }
			set
			{
				isDynamicQuery = value;
				OnPropertyChanged(() => IsDynamicQuery);
			}
		}

		public BindableCollection<string> DynamicOptions { get; set; }

		private string dynamicSelectedOption;
		public string DynamicSelectedOption
		{
			get { return dynamicSelectedOption; }
			set
			{
				dynamicSelectedOption = value;
				switch (dynamicSelectedOption)
				{
					case "AllDocs":
						IndexName = "dynamic";
						break;
					default:
						IndexName = "dynamic/" + dynamicSelectedOption;
						break;
				}

			    if (dynamicSelectedOption != "AllDocs")
			    {
			        BeginUpdateFieldsAndSortOptions(dynamicSelectedOption);
			    }
			    else
                {
                    SortBy.Clear();
                    SortByOptions.Clear();
                    QueryIndexAutoComplete = new QueryIndexAutoComplete(new string[0]);
                    RestoreHistory();
                }

			    OnPropertyChanged(() => DynamicSelectedOption);
			}
		}

	    private void BeginUpdateFieldsAndSortOptions(string collection)
	    {
	        DatabaseCommands.QueryAsync("Raven/DocumentsByEntityName",
	                                    new IndexQuery() {Query = "Tag:" + collection, Start = 0, PageSize = 1}, null)
	            .ContinueOnSuccessInTheUIThread(result =>
	                                                {
                                                        if (result.Results.Count > 0)
                                                        {
                                                            var fields = DocumentHelpers.GetPropertiesFromJObjects(result.Results, includeNestedProperties:true, includeMetadata:false, excludeParentPropertyNames:true)
                                                                .ToList();

                                                            SetSortByOptions(fields);
                                                            QueryIndexAutoComplete = new QueryIndexAutoComplete(fields);
                                                            RestoreHistory();
                                                        }
	                                                });
	    }

	    public QueryModel()
		{
			ModelUrl = "/query";
			
            CollectionSource = new QueryDocumentsCollectionSource();
		    Observable.FromEventPattern<QueryStatisticsUpdatedEventArgs>(h => CollectionSource.QueryStatisticsUpdated += h,
		                                                                 h => CollectionSource.QueryStatisticsUpdated -= h)
		        .SampleResponsive(TimeSpan.FromSeconds(0.5))
                .TakeUntil(Unloaded)
		        .ObserveOnDispatcher()
		        .Subscribe(e =>
		                       {
		                           QueryTime = e.EventArgs.QueryTime;
		                           Results = e.EventArgs.Statistics;
		                       });
		    Observable.FromEventPattern<QueryErrorEventArgs>(h => CollectionSource.QueryError += h,
		                                                     h => CollectionSource.QueryError -= h)
		        .ObserveOnDispatcher()
		        .Subscribe(e => HandleQueryError(e.EventArgs.Exception));

		    DocumentsResult = new DocumentsModel(CollectionSource)
		                          {
                                      Header = "Results",
                                      SkipAutoRefresh = true,
                                      DocumentNavigatorFactory = (id, index) => DocumentNavigator.Create(id, index, IndexName, CollectionSource.TemplateQuery),
		                          };

            Query = new Observable<string>();
            QueryErrorMessage = new Observable<string>();
            IsErrorVisible = new Observable<bool>();

			SortBy = new BindableCollection<StringRef>(x => x.Value);
		    SortBy.CollectionChanged += HandleSortByChanged;
			SortByOptions = new BindableCollection<string>(x => x);
			Suggestions = new BindableCollection<FieldAndTerm>(x => x.Field);
			DynamicOptions = new BindableCollection<string>(x => x) {"AllDocs"};
		}

	    private void HandleQueryError(Exception exception)
	    {
	        if (exception is AggregateException)
	        {
	            exception = ((AggregateException) exception).ExtractSingleInnerException();
	        }

	        QueryErrorMessage.Value = exception.Message;
	        IsErrorVisible.Value = true;
	    }

	    public void ClearQueryError()
	    {
	        QueryErrorMessage.Value = string.Empty;
	        IsErrorVisible.Value = false;
	    }

	    private void HandleSortByChanged(object sender, NotifyCollectionChangedEventArgs e)
	    {
	        if (e.Action == NotifyCollectionChangedAction.Add)
	        {
	            (e.NewItems[0] as StringRef).PropertyChanged += delegate { Requery(); };
	        }
	    }

	    private void Requery()
	    {
	        Execute.Execute(null);
	    }

	    public override void LoadModelParameters(string parameters)
		{
			var urlParser = new UrlParser(parameters);

			if (urlParser.GetQueryParam("mode") == "dynamic")
			{
				IsDynamicQuery = true;
				DatabaseCommands.GetTermsAsync("Raven/DocumentsByEntityName", "Tag", "", 100)
					.ContinueOnSuccessInTheUIThread(collections =>
					                       {
					                           DynamicOptions.Match(new[] {"AllDocs"}.Concat(collections).ToArray());
                                               DynamicSelectedOption = DynamicOptions[0];
					                       });
				return;
			}

			IndexName = urlParser.Path.Trim('/');

			DatabaseCommands.GetIndexAsync(IndexName)
				.ContinueOnUIThread(task =>
				{
					if (task.IsFaulted || task.Result  == null)
					{
						IndexDefinitionModel.HandleIndexNotFound(IndexName);
						return;
					}
                    var fields = task.Result.Fields;
					QueryIndexAutoComplete = new QueryIndexAutoComplete(fields, IndexName, Query);
					
					const string spatialindexGenerate = "SpatialIndex.Generate";
					IsSpatialQuerySupported =
                        task.Result.Maps.Any(x => x.Contains(spatialindexGenerate)) ||
                        (task.Result.Reduce != null && task.Result.Reduce.Contains(spatialindexGenerate));
				    HasTransform = !string.IsNullOrEmpty(task.Result.TransformResults);

					SetSortByOptions(fields);
                    RestoreHistory();
				}).Catch();
		}

	    public bool HasTransform
	    {
            get { return hasTransform; }
            private set
            {
                hasTransform = value;
                OnPropertyChanged(() => HasTransform);
            }
	    }

	    public void RememberHistory()
	    {
	        var state = PerDatabaseState.QueryState.GetState(IndexName);

	        state.Query = Query.Value;
            state.SortOptions.Clear();
	        state.SortOptions.AddRange(SortBy.Select(r => r.Value).ToList());
		}

		public void RestoreHistory()
		{
            var state = PerDatabaseState.QueryState.GetState(IndexName);

            internalUpdate = true;

            Query.Value = state.Query;
            SortBy.Clear();

		    foreach (var sortOption in state.SortOptions)
		    {
		        if (SortByOptions.Contains(sortOption))
		        {
		            SortBy.Add(new StringRef() { Value = sortOption});
		        }
		    }
		    internalUpdate = false;

            Requery();
		}

		public ICommand Execute { get { return executeQuery ?? (executeQuery = new ExecuteQueryCommand(this)); } }

        public Observable<string> QueryErrorMessage { get; private set; }
        public Observable<bool> IsErrorVisible { get; private set; } 
		public Observable<string> Query { get; private set; }

		private TimeSpan queryTime;
	    private bool internalUpdate;
	    public TimeSpan QueryTime
		{
			get { return queryTime; }
			set
			{
				queryTime = value;
				OnPropertyChanged(() => QueryTime);
			}
		}

	    public RavenQueryStatistics Results
		{
			get { return results; }
			set
			{
				results = value;
				OnPropertyChanged(() => Results);
			}
		}


		public DocumentsModel DocumentsResult { get; private set; }

		public BindableCollection<FieldAndTerm> Suggestions { get; private set; }
		public ICommand RepairTermInQuery
		{
			get { return new RepairTermInQueryCommand(this); }
		}

		private class RepairTermInQueryCommand : Command
		{
			private readonly QueryModel model;
			private FieldAndTerm fieldAndTerm;

			public RepairTermInQueryCommand(QueryModel model)
			{
				this.model = model;
			}

			public override bool CanExecute(object parameter)
			{
				fieldAndTerm = parameter as FieldAndTerm;
				return fieldAndTerm != null;
			}

			public override void Execute(object parameter)
			{
				model.Query.Value = model.Query.Value.Replace(fieldAndTerm.Term, fieldAndTerm.SuggestedTerm);
				model.Requery();
			}
		}

		public string PageTitle
		{
			get { return "Query Index"; }
		}
	}
}
