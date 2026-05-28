window.scrollPrompter = (position) => {
    const element = document.querySelector('.prompter-content');
    if (element) {
        element.scrollTop = position;
    }
};
