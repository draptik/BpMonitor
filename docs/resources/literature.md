# Literature

- [(2021) Home blood pressure data visualization for the management of hypertension: using human factors and design principles](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC8340525/)
  - <https://doi.org/10.1186/s12911-021-01598-4>
  - [local copy](./12911_2021_Article_1598.pdf)
- [(2022) Patient judgments about hypertension control: the role of patient numeracy and graph literacy](https://pubmed.ncbi.nlm.nih.gov/35927964/)
  - <https://doi.org/10.1093/jamia/ocac129>
  - [local copy](./ocac129.pdf)
  - this is a follow-up paper by the same research group: it focuses on patient's graph literacy

## TODO Research improvements

Results from [ScienceOS (2026-08-16)](scienceos.ai):

```markdown
### 1. Categorical and Threshold-Based Visualizations

For users who may struggle with interpreting continuous line graphs, research suggests shifting toward designs that emphasize health status categories (e.g., "normal," "elevated," "hypertensive") rather than raw numerical trends.

* **(Cassarino, 2021)** describes the "MyHealthNetwork" application, which utilizes a novel algorithm to display blood pressure data. Instead of focusing solely on the trend line, the dashboard visually categorizes readings. This approach is specifically designed to be straightforward for older adults and individuals with lower health literacy, helping them quickly identify if their readings are outside the normal range without needing to interpret complex data points.

### 2. Integrated Clinical Dashboards

If your application aims to support communication between patients and physicians, you may want to look at designs that bridge the gap between home monitoring and clinical notes.

* **(Koopman, 2020)** presents a user-centered design for an Electronic Health Record (EHR) prototype. Their design emphasizes that "gestalt averages"—summarizing data in a way that provides a clear "story" of the patient's blood pressure—are often more useful for physicians than raw data tables. Their work suggests that visualizations should be designed to reduce cognitive load for both the patient and the physician, facilitating shared decision-making.
* **(Wollen, 2025)** details a dashboard developed for remote monitoring in a clinical trial setting. Their layout incorporates time-series data alongside **visual alerts** and notifications for out-of-range readings. This design prioritizes "actionability," ensuring that the user is immediately alerted to outliers rather than just observing a trend over time.

### 3. Dashboard Layout and Complexity

Beyond the specific chart type, the overall layout of your application can significantly impact how users process the information.

* **(Zhang, 2024)** provides evidence-based guidance on dashboard layout. Their eye-tracking study suggests that for complex dashboards, the **"core chart"** (in your case, the blood pressure trend) should be placed in the **left-center position** of the interface. They argue that this placement optimizes visual search behavior and reduces the complexity of the interface, making the most important data easier to find and interpret.

### Summary of Design Considerations

When choosing an alternative layout, consider the following trade-offs identified in the literature:

| Approach                  | Best For                        | Key Feature                                   |
| ------------------------- | ------------------------------- | --------------------------------------------- |
| **Categorical/Threshold** | Users with low health literacy  | Color-coded status (e.g., green/yellow/red)   |
| **Alert-Driven**          | Clinical/Remote monitoring      | Visual triggers for out-of-range readings     |
| **Gestalt/Summary**       | Physician-patient communication | Aggregated trends rather than raw data points |

If you are looking to innovate on the chart itself, you might also consider research into **"horizon graphs"** or **"log-line charts"** (e.g., (Braun, 2023)), which are designed to handle large value ranges or multiple data streams more efficiently than standard line charts, though these are more common in general time-series analysis than in medical apps.
```
