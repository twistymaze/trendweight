import { useEffect } from "react";
import { useIsFetching } from "react-query";
import progress from "~/lib/core/progress";

const BackgroundQueryProgress = () => {
  const queryInProgress = useIsFetching();

  useEffect(() => {
    progress.setFetching(queryInProgress);
  }, [queryInProgress]);

  return null;
};

export default BackgroundQueryProgress;
