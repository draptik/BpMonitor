# TODOs

A loose collection of TODOs I want to tackle at some point in time.

## Layout/Design, especially mobile

Double-check if this is still relevant:

- The trend readings table is too wide on mobile. Some ideas
  - use "cards" instead of row (not practical for yearly view)
  - or: shorten time stamp string
  - or: hide comment

## Features

- some science behind the visualization:
  [Home blood pressure data visualization for the management of hypertension: using human factors and design principles](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC8340525/)
  (as reference, I included the paper in the docs folder: [pdf](./docs/resources/12911_2021_Article_1598.pdf))
- Replace the recent display with something like fig 5 in the paper above
  - Add a smoothing curve (for example using LOWESS algorithm)
  - add a moving window which always shows xx days (see [fig. 5](https://pmc.ncbi.nlm.nih.gov/articles/PMC8340525/figure/Fig5/) in the paper above)?
  - add another display above the graph of the sys/dias values like in fig 5
- Exploratory data analysis using [Jupyter Notebooks](https://jupyter.org/).
  This can be integrated some how into this application, or more realistically, a separate application.
