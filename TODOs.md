# TODOs

A loose collection of TODOs I want to tackle at some point in time.

## Layout/Design, especially mobile

The mobile layout has room for improvement...

- The trend readings table is too wide on mobile
  - use "cards" instead of row (not practical for yearly view)
  - or: shorten time stamp string
  - or: hide comment

## Features

- Add "healthy boundaries" for Sys/Dia to the trend charts.
  - these boundaries should be configurable by the user
  - see my older [blood pressure charting](https://github.com/draptik/blood-pressure-charting) project for an example
  - and for some science behind the visualization:
    [Home blood pressure data visualization for the management of hypertension: using human factors and design principles](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC8340525/)
    (as reference, I included the paper in the docs folder: [pdf](./docs/resources/12911_2021_Article_1598.pdf))
  - based on that paper: Add a smoothing curve (for example using LOWESS algorithm)
  - maybe also add a moving window which always shows xx days (see [fig. 5](https://pmc.ncbi.nlm.nih.gov/articles/PMC8340525/figure/Fig5/) in the paper above)?
- Exploratory data analysis using [Jupyter Notebooks](https://jupyter.org/).
  This can be integrated some how into this application, or more realistically, a separate application.
