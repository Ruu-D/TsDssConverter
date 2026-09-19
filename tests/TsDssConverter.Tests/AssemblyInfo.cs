// The language of the interface is one switch for the whole program (Localizer.Current).
// Tests that change it must not run at the same time as tests that read texts in the default language,
// so the tests run one after the other. (It costs a few seconds.)
[assembly: CollectionBehavior(DisableTestParallelization = true)]
